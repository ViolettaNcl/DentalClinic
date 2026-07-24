import { apiFetch } from './apiClient.js';

// Кэш переводов отзывов в рамках вкладки браузера — чтобы не дёргать
// сервер повторно, если пользователь переключает язык туда-обратно.
const memoryCache = new Map(); // `${id}:${lang}` -> translated text

// Если несколько карточек отрисовываются одновременно (например, карусель на
// главной), не шлём параллельно несколько одинаковых запросов на один и тот
// же отзыв — ждём общий промис, чтобы не словить rate-limit от API перевода.
const inFlight = new Map(); // key -> Promise<string>

function cacheKey(id, lang) {
    // v2: старые записи (сохранённые до этого исправления, когда неудачная
    // попытка перевода могла закэшироваться как будто это перевод) больше
    // не подхватываются — отзыв будет переведён заново.
    return `v2:${id}:${lang}`;
}

/**
 * Вернуть текст отзыва на нужном языке.
 * Русский считается языком-оригиналом отзывов — переводить его не нужно.
 * Для остальных языков текст переводится через backend (см. ReviewController.Translate)
 * и результат кэшируется как в памяти, так и в sessionStorage (переживает
 * обновление страницы в рамках той же вкладки).
 */
async function translateReviewText(id, originalText, lang) {
    if (lang === 'ru' || !originalText) return originalText;

    const key = cacheKey(id, lang);
    if (memoryCache.has(key)) return memoryCache.get(key);
    if (inFlight.has(key)) return inFlight.get(key);

    const promise = (async () => {
        const storageKey = `reviewTranslate:${key}`;
        try {
            const stored = sessionStorage.getItem(storageKey);
            if (stored !== null) {
                memoryCache.set(key, stored);
                return stored;
            }
        } catch { /* sessionStorage может быть недоступен (приватный режим) — не критично */ }

        try {
            const res = await apiFetch('/review/translate', {
                method: 'POST',
                body: JSON.stringify({ reviewId: id, text: originalText, targetLang: lang })
            });
            const translated = res?.text || originalText;

            // Кэшируем только реально успешный перевод — иначе одна неудачная
            // попытка (например, из-за 429 от API) навсегда застревала бы в кэше,
            // и отзыв оставался бы непереведённым до конца сессии в браузере.
            if (translated !== originalText) {
                memoryCache.set(key, translated);
                try { sessionStorage.setItem(storageKey, translated); } catch { /* ignore */ }
            }
            return translated;
        } catch (err) {
            console.error('review translate error:', err);
            return originalText; // при ошибке показываем оригинал — лучше, чем пустая карточка
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
 * Пройтись по уже отрисованным карточкам отзывов и подставить перевод
 * текста на текущий язык. Карточки должны иметь элемент с атрибутом
 * data-review-text-id="{id}" внутри которого лежит escaped-текст отзыва.
 * originals — Map(id -> исходный текст, ДО экранирования).
 */
async function applyReviewTranslations(container, originals, lang) {
    if (!container || lang === 'ru') return;

    const nodes = container.querySelectorAll('[data-review-text-id]');
    await Promise.all(Array.from(nodes).map(async (node) => {
        const id = node.dataset.reviewTextId;
        const original = originals.get(id);
        if (original === undefined) return;
        const translated = await translateReviewText(id, original, lang);
        if (translated && translated !== original) {
            node.textContent = translated;
        }
    }));
}

export { translateReviewText, applyReviewTranslations };