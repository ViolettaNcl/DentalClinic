import { t } from '../core/i18n.js';
import { escapeHtmlAttribute } from './htmlAttributeSafety.js';

function _getToastContainer() {
    let el = document.getElementById('toast-container');
    if (!el) {
        el = document.createElement('div');
        el.id = 'toast-container';
        el.className = 'toast-container';
        document.body.appendChild(el);
    }
    return el;
}

/**
 * Ставит сообщение "в очередь", чтобы показать его уже после window.location.href —
 * иначе тост создаётся и тут же уничтожается вместе со страницей, толком не успев
 * появиться (например, тост "Вход успешен" перед редиректом в кабинет).
 * options — те же необязательные поля, что принимает showToast (icon, title, celebrate).
 */
export function queueToast(message, type = 'success', options = {}) {
    try {
        sessionStorage.setItem('pendingToast', JSON.stringify({ message, type, options }));
    } catch { /* sessionStorage недоступен — просто теряем тост, не критично */ }
}

function _flushPendingToast() {
    let raw;
    try {
        raw = sessionStorage.getItem('pendingToast');
    } catch {
        return;
    }
    if (!raw) return;
    sessionStorage.removeItem('pendingToast');
    try {
        const { message, type, options } = JSON.parse(raw);
        // ждём кадр, чтобы контейнер тостов и стили страницы точно были готовы
        requestAnimationFrame(() => showToast(message, type, undefined, options));
    } catch { /* битые данные в sessionStorage — просто игнорируем */ }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', _flushPendingToast, { once: true });
} else {
    _flushPendingToast();
}

// Бэкенд сам добавляет эмодзи в начало сообщений (например, "✅ Вход успешен!"),
// а тост уже рисует свою иконку в кружке — без очистки получались две галочки/два смайла подряд.
const LEADING_EMOJI_RE = /^[\p{Emoji_Presentation}\p{Extended_Pictographic}\u2600-\u27BF\uFE0F\s]+/u;
function stripLeadingEmoji(text) {
    return String(text ?? '').replace(LEADING_EMOJI_RE, '').trim();
}

const TOAST_TYPES = new Set(['success', 'error', 'info']);
function normalizeToastType(type) {
    return TOAST_TYPES.has(type) ? type : 'info';
}

function normalizeToastDuration(duration) {
    const numeric = Number(duration);
    if (!Number.isFinite(numeric)) return 5500;
    return Math.min(30000, Math.max(1000, Math.round(numeric)));
}

/**
 * options:
 *   icon      — переопределить иконку (эмодзи)
 *   title     — переопределить заголовок
 *   celebrate — включить праздничное оформление (вход/регистрация) явно,
 *               без угадывания по тексту сообщения
 */
export function showToast(message, type = 'info', duration = 5500, options = {}) {
    const container = _getToastContainer();
    const safeType = normalizeToastType(type);
    duration = normalizeToastDuration(duration);

    const icons = { success: '✅', error: '⚠️', info: 'ℹ️' };
    const titles = {
        success: t('ui_toast_success', 'Готово'),
        error: t('ui_toast_error', 'Ошибка'),
        info: t('ui_toast_info', 'Информация')
    };

    const cleanMessage = stripLeadingEmoji(message);

    // Особый случай: сообщение содержит номер телефона (например, просьба
    // позвонить администратору) — выделяем его отдельным акцентом. Раньше
    // это определялось по русскому слову "позвон", но текст теперь может
    // прийти на любом языке, поэтому ориентируемся только на сам номер —
    // этого паттерна достаточно, чтобы не путать с обычными числами в тексте.
    const phoneMatch = cleanMessage.match(/(\+?\d[\d\s()-]{7,}\d)/);
    const isCallHint = !!phoneMatch;

    let icon = icons[safeType];
    let title = titles[safeType];
    let bodyHtml = escapeHtml(cleanMessage);

    // Более конкретные заголовки и увеличенное время показа для типовых сообщений.
    // Явный options.title (передаётся вызывающим кодом через t(...)) всегда в приоритете;
    // регэкспы ниже — лишь запасной вариант для старых мест, где title не передан явно.
    let isCelebrate = false;
    if (safeType === 'success') {
        if (options?.celebrate) { isCelebrate = true; duration = 6000; }
        else if (/профиль обновл/i.test(cleanMessage)) title = t('ui_profile_updated', 'Профиль обновлён');
        else if (/пароль.*измен/i.test(cleanMessage)) title = t('ui_password_changed', 'Пароль изменён');
    }

    if (options?.icon) icon = String(options.icon);
    if (options?.title) title = String(options.title);

    if (isCallHint) {
        icon = '📞';
        if (!options?.title) title = t('ui_call_needed_title', 'Нужен звонок в клинику');
        const phone = phoneMatch[1];
        const before = cleanMessage.slice(0, cleanMessage.indexOf(phone)).replace(/[—-]\s*$/, '').trim();
        bodyHtml = `${escapeHtml(before)}<br><span class="toast-phone">${escapeHtml(phone)}</span>`;
    }

    const toast = document.createElement('div');
    const isWave = isCelebrate && icon === '👋';
    toast.className = `toast toast-${safeType}${isCallHint ? ' toast-call' : ''}${isCelebrate ? ' toast-celebrate' : ''}${isWave ? ' toast-wave' : ''}`;

    // The queued toast payload lives in sessionStorage and is therefore client-controlled.
    // Keep its title/icon/type/duration out of HTML-string interpolation. Only bodyHtml is
    // assigned as markup, and every dynamic segment inside it has already been escaped.
    toast.innerHTML = `
        <span class="toast-icon"></span>
        <div class="toast-body">
            <div class="toast-title"></div>
            <div class="toast-message"></div>
        </div>
        <button type="button" class="toast-close">✕</button>
        <div class="toast-progress"></div>
    `;

    toast.querySelector('.toast-icon').textContent = icon;
    toast.querySelector('.toast-title').textContent = title;
    toast.querySelector('.toast-message').innerHTML = bodyHtml;
    toast.querySelector('.toast-close').setAttribute('aria-label', t('ui_close', 'Закрыть'));
    toast.querySelector('.toast-progress').style.animationDuration = `${duration}ms`;

    if (isCelebrate) {
        const bits = ['✨', '🎊', '⭐', '💫'];
        const iconEl = toast.querySelector('.toast-icon');
        for (let i = 0; i < 5; i++) {
            const dx = (Math.random() * 70 - 35).toFixed(0);
            const dy = (-30 - Math.random() * 40).toFixed(0);
            const bit = document.createElement('span');
            bit.className = 'toast-confetti';
            bit.textContent = bits[i % bits.length];
            bit.style.left = '10px';
            bit.style.top = '8px';
            bit.style.setProperty('--confetti-end', `translate(${dx}px, ${dy}px)`);
            bit.style.animationDelay = `${i * 60}ms`;
            iconEl.after(bit);
        }
    }

    container.appendChild(toast);

    const remove = () => {
        toast.classList.add('toast-hide');
        toast.addEventListener('animationend', () => toast.remove(), { once: true });
    };

    const timer = setTimeout(remove, duration);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(timer);
        remove();
    });
}

export function showSuccess(message, options = {}) {
    showToast(message, 'success', undefined, options);
}

export function showError(message, options = {}) {
    showToast(message, 'error', undefined, options);
}

/**
 * Красивое модальное окно подтверждения взамен нативного confirm().
 * Возвращает Promise<boolean> — true, если пользователь нажал "подтвердить".
 */
export function showConfirm(message, options = {}) {
    const {
        title = t('ui_confirm_title', 'Подтверждение'),
        confirmText = t('ui_confirm_yes', 'Да, продолжить'),
        cancelText = t('ui_confirm_cancel', 'Отмена'),
        danger = false,
        icon = danger ? '⚠️' : '❓'
    } = options;

    return new Promise(resolve => {
        const wrap = document.createElement('div');
        wrap.className = 'panel-modal panel-confirm-modal';
        wrap.innerHTML = `
            <div class="panel-modal-backdrop"></div>
            <div class="panel-modal-dialog panel-confirm-dialog${danger ? ' panel-confirm-danger' : ''}">
                <div class="panel-confirm-icon">${escapeHtml(icon)}</div>
                <h3 class="panel-confirm-title">${escapeHtml(title)}</h3>
                <p class="panel-confirm-message">${escapeHtml(message)}</p>
                <div class="panel-modal-footer panel-confirm-footer">
                    <button type="button" class="panel-btn-secondary" data-action="cancel">${escapeHtml(cancelText)}</button>
                    <button type="button" class="panel-btn-primary${danger ? ' panel-btn-danger' : ''}" data-action="confirm">${escapeHtml(confirmText)}</button>
                </div>
            </div>
        `;
        document.body.appendChild(wrap);

        let settled = false;
        const close = (result) => {
            if (settled) return;
            settled = true;
            document.removeEventListener('keydown', onKeyDown);
            wrap.classList.add('panel-modal-closing');
            wrap.addEventListener('animationend', () => wrap.remove(), { once: true });
            resolve(result);
        };

        const onKeyDown = (e) => {
            if (e.key === 'Escape') close(false);
        };
        document.addEventListener('keydown', onKeyDown);

        wrap.querySelector('[data-action="confirm"]').addEventListener('click', () => close(true));
        wrap.querySelector('[data-action="cancel"]').addEventListener('click', () => close(false));
        wrap.querySelector('.panel-modal-backdrop').addEventListener('click', () => close(false));
    });
}

/**
 * Экранирует HTML-спецсимволы перед вставкой пользовательских данных в innerHTML.
 * В том числе безопасен для значений в quoted HTML-атрибутах: кавычки тоже
 * кодируются, поэтому данные не могут закрыть data-, aria- или title-атрибут
 * и добавить новый обработчик события или произвольный атрибут.
 */
export function escapeHtml(str) {
    return escapeHtmlAttribute(str);
}

/**
 * Рисует простую пагинацию (‹ Стр. X из Y ›) в переданный контейнер
 * и вешает обработчики на кнопки "назад"/"вперёд".
 * container   — DOM-элемент, куда рендерить controls
 * page        — текущая страница (с 1)
 * totalItems  — всего элементов в полном (нефильтрованном) списке
 * pageSize    — сколько показываем на одной странице
 * onPageChange(newPage) — вызывается при клике на кнопку
 */
export function renderPagination(container, { page, totalItems, pageSize, onPageChange }) {
    if (!container) return;

    const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));

    if (totalItems === 0 || totalPages <= 1) {
        container.innerHTML = '';
        return;
    }

    const pageInfo = t('ui_pagination_info', 'Стр. {page} из {total}')
        .replace('{page}', page)
        .replace('{total}', totalPages);

    container.innerHTML = `
        <div class="pagination">
            <button type="button" class="pagination-btn" data-dir="prev" ${page <= 1 ? 'disabled' : ''}>‹ ${escapeHtml(t('ui_pagination_prev', 'Назад'))}</button>
            <span class="pagination-info">${escapeHtml(pageInfo)}</span>
            <button type="button" class="pagination-btn" data-dir="next" ${page >= totalPages ? 'disabled' : ''}>${escapeHtml(t('ui_pagination_next', 'Вперёд'))} ›</button>
        </div>`;

    container.querySelector('[data-dir="prev"]')?.addEventListener('click', () => {
        if (page > 1) onPageChange(page - 1);
    });
    container.querySelector('[data-dir="next"]')?.addEventListener('click', () => {
        if (page < totalPages) onPageChange(page + 1);
    });
}
