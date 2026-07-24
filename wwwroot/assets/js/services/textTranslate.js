import { apiFetch } from './apiClient.js';

// Кэш в рамках вкладки браузера — одинаковый текст переводим только один раз
// за сессию, дальше берём из памяти/sessionStorage.
const memoryCache = new Map(); // `${kind}:${lang}:${text}` -> translated text

// Если несколько мест на странице одновременно просят перевод ОДНОГО и того же
// текста (например, имя врача повторяется и в таблице, и в карточке аналитики),
// не шлём несколько параллельных запросов к API — ждём один и тот же промис.
// Это не только экономит запросы, но и снижает риск словить rate-limit от
// переводческого API при одновременной отрисовке нескольких блоков.
const inFlight = new Map(); // key -> Promise<string>

// Внешний переводческий API (Gemini) имеет небольшую квоту и не любит,
// когда на него сразу прилетает много параллельных запросов — при отрисовке
// таблицы записей это может быть сразу десяток комментариев/имён одновременно.
// Поэтому реальные сетевые запросы идут через простую очередь: не больше
// MAX_CONCURRENT_REQUESTS одновременно, остальные ждут своей очереди — это
// заметно снижает число "отказов" перевода без усложнения кода на вызывающей
// стороне (комментарии, имена — им не нужно ничего знать про очередь).
const MAX_CONCURRENT_REQUESTS = 3;
let activeRequests = 0;
const requestQueue = [];

function acquireRequestSlot() {
    if (activeRequests < MAX_CONCURRENT_REQUESTS) {
        activeRequests++;
        return Promise.resolve();
    }
    return new Promise(resolve => requestQueue.push(resolve));
}

function releaseRequestSlot() {
    const next = requestQueue.shift();
    if (next) {
        next(); // слот сразу передаётся следующему в очереди, счётчик не меняется
    } else {
        activeRequests--;
    }
}

async function requestTranslation(text, lang, kind) {
    await acquireRequestSlot();
    try {
        const res = await apiFetch('/translate', {
            method: 'POST',
            body: JSON.stringify({ text, targetLang: lang, kind })
        });
        return res?.text || text;
    } finally {
        releaseRequestSlot();
    }
}

function cacheKey(kind, lang, text) {
    // v2: старые записи (сохранённые до этого исправления, когда неудачная
    // попытка перевода могла закэшироваться как будто это перевод) больше
    // не подхватываются — текст будет переведён заново.
    return `v2:${kind}:${lang}:${text}`;
}

/**
 * Перевести (или транслитерировать, для kind="name") произвольный текст на
 * нужный язык через backend (см. TranslateController).
 *
 * По умолчанию считаем русский языком-оригиналом данных в базе — не
 * переводим, если lang === 'ru' (экономим запрос: данные и так уже на
 * русском). Для случаев, где исходный текст сам мог быть НЕ русским
 * (например, история чат-бота — сообщение могло появиться на любом языке),
 * передайте { assumeRussianSource: false }, тогда перевод "туда-обратно"
 * на русский тоже будет выполнен через API.
 */
async function translateText(text, lang, kind = 'text', { assumeRussianSource = true } = {}) {
    if (!text) return text;
    if (lang === 'ru' && assumeRussianSource) return text;

    const key = cacheKey(kind, lang, text);
    if (memoryCache.has(key)) return memoryCache.get(key);
    if (inFlight.has(key)) return inFlight.get(key);

    const promise = (async () => {
        const storageKey = `textTranslate:${key}`;
        try {
            const stored = sessionStorage.getItem(storageKey);
            if (stored !== null) {
                memoryCache.set(key, stored);
                return stored;
            }
        } catch { /* приватный режим браузера — не критично */ }

        try {
            let translated = await requestTranslation(text, lang, kind);

            // Если текст не изменился, хотя язык другой — скорее всего это
            // была неудачная попытка (внешний API временно перегружен или
            // словил лимит), а не реальный "уже переведено". Тихо повторяем
            // ещё пару раз с небольшой паузой — обычно вторая-третья попытка
            // уже проходит, и пользователь просто не замечает первой неудачи.
            let attempt = 1;
            while (translated === text && attempt < 3) {
                await new Promise(resolve => setTimeout(resolve, 500 * attempt));
                translated = await requestTranslation(text, lang, kind);
                attempt++;
            }

            // Кэшируем ТОЛЬКО реально успешный перевод (отличный от оригинала).
            // Если сервер так и не смог перевести после всех попыток и вернул
            // текст как есть — не запоминаем это как "перевод", иначе одна
            // неудачная попытка застревала бы в кэше на всю сессию и текст
            // оставался бы непереведённым до перезагрузки вкладки.
            if (translated !== text) {
                memoryCache.set(key, translated);
                try { sessionStorage.setItem(storageKey, translated); } catch { /* ignore */ }
            }
            return translated;
        } catch (err) {
            console.error('translate error:', err);
            return text; // при ошибке — показываем оригинал, лучше чем пустая ячейка
        }
    })();

    inFlight.set(key, promise);
    try {
        return await promise;
    } finally {
        inFlight.delete(key);
    }
}

/**
 * Пройтись по контейнеру и перевести все элементы с data-translate-text
 * (обычный текст, например комментарий к записи) и data-translate-name
 * (имя человека — транслитерируется, а не переводится дословно).
 * Значение атрибута — это исходный (русский) текст.
 */
async function applyTextTranslations(container, lang) {
    if (!container || lang === 'ru') return;

    const textNodes = [
        ...(container.matches?.('[data-translate-text]') ? [container] : []),
        ...container.querySelectorAll('[data-translate-text]')
    ];
    const nameNodes = [
        ...(container.matches?.('[data-translate-name]') ? [container] : []),
        ...container.querySelectorAll('[data-translate-name]')
    ];

    await Promise.all([
        ...textNodes.map(async (node) => {
            const original = node.dataset.translateText;
            if (!original) return;
            const translated = await translateText(original, lang, 'text');
            if (translated && translated !== original) node.textContent = translated;
        }),
        ...nameNodes.map(async (node) => {
            const original = node.dataset.translateName;
            if (!original) return;
            const translated = await translateText(original, lang, 'name');
            if (translated && translated !== original) node.textContent = translated;
        }),
    ]);
}

export { translateText, applyTextTranslations };