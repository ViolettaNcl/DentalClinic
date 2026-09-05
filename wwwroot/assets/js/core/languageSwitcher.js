// =====================================================
// 🌐 ПЕРЕКЛЮЧАТЕЛЬ ЯЗЫКА (UI-кнопка в шапке)
// Кнопка и дропдаун теперь лежат прямо в разметке header.html
// (#lang-switcher / #lang-btn / #lang-dropdown) — этот файл только
// заполняет дропдаун вариантами и навешивает обработчики, но НЕ
// создаёт и не двигает сам элемент по DOM, поэтому его позиция в
// шапке всегда та, что задана в HTML/CSS (справа, рядом с Contact).
// Сама загрузка словарей и хранение состояния вынесены в
// core/i18n.js.
// =====================================================

import { getLang, setLang, ready } from './i18n.js';

const LANGUAGES = [
    { code: 'ru', label: 'РУ', full: 'Русский' },
    { code: 'fr', label: 'FR', full: 'Français' },
    { code: 'en', label: 'EN', full: 'English' },
    { code: 'el', label: 'ΕΛ', full: 'Ελληνικά' },
    { code: 'ar', label: 'ع', full: 'العربية' },
];

const ATTRIBUTE_TRANSLATIONS = [
    ['data-i18n-placeholder', 'placeholder'],
    ['data-i18n-aria-label', 'aria-label'],
    ['data-i18n-title', 'title'],
    ['data-i18n-alt', 'alt'],
    ['data-i18n-value', 'value'],
];

class LanguageSwitcher {
    constructor() {
        this.currentLang = getLang();
    }

    async init() {
        this._render();
        await ready; // словарь текущего языка уже загружен модулем i18n.js
        await this._apply(this.currentLang);
    }

    // ── Заполняем уже существующую в HTML кнопку/дропдаун ──
    _render() {
        const wrapper = document.getElementById('lang-switcher');
        const btn = document.getElementById('lang-btn');
        const dropdown = document.getElementById('lang-dropdown');
        if (!wrapper || !btn || !dropdown) return; // header.html не содержит разметку — молча выходим

        const current = LANGUAGES.find(l => l.code === this.currentLang) || LANGUAGES[0];
        btn.querySelector('.lang-btn__label').textContent = current.label;
        btn.title = current.full;

        dropdown.innerHTML = '';
        LANGUAGES.forEach(lang => {
            const li = document.createElement('li');
            li.className = 'lang-option' + (lang.code === this.currentLang ? ' lang-option--active' : '');
            li.textContent = lang.full;
            li.dataset.lang = lang.code;
            li.addEventListener('click', () => this._select(lang.code, wrapper, btn, dropdown));
            dropdown.appendChild(li);
        });

        btn.addEventListener('click', () => {
            wrapper.classList.toggle('lang-switcher--open');
        });

        document.addEventListener('click', (e) => {
            if (!wrapper.contains(e.target)) {
                wrapper.classList.remove('lang-switcher--open');
            }
        });

        this._btn = btn;
    }

    // ── Клик по языку в дропдауне ──
    async _select(code, wrapper, btn, dropdown) {
        this.currentLang = code;

        const lang = LANGUAGES.find(l => l.code === code);
        btn.querySelector('.lang-btn__label').textContent = lang.label;
        btn.title = lang.full;

        dropdown.querySelectorAll('.lang-option').forEach(li => {
            li.classList.toggle('lang-option--active', li.dataset.lang === code);
        });

        wrapper.classList.remove('lang-switcher--open');

        await this._apply(code);
    }

    // ── Применяем перевод к статичным элементам страницы + сообщаем
    //    остальным модулям (уведомления, отзывы, кабинет и т.д.),
    //    что язык сменился, через core/i18n.js ──
    async _apply(code) {
        document.documentElement.lang = code;
        document.documentElement.dir = code === 'ar' ? 'rtl' : 'ltr';

        const dict = await setLang(code); // грузит словарь + уведомляет всех подписчиков onLanguageChange
        if (!dict) return;

        // Обычный текст
        document.querySelectorAll('[data-i18n]').forEach(el => {
            const key = el.dataset.i18n;
            if (dict[key] !== undefined) {
                el.textContent = dict[key];
            }
        });

        // Переводимые атрибуты: placeholder + accessibility/tooltip/media labels.
        // Новые data-i18n-* атрибуты можно добавлять постепенно без изменения
        // поведения существующей разметки.
        ATTRIBUTE_TRANSLATIONS.forEach(([dataAttribute, targetAttribute]) => {
            document.querySelectorAll(`[${dataAttribute}]`).forEach(el => {
                const key = el.getAttribute(dataAttribute);
                if (key && dict[key] !== undefined) {
                    el.setAttribute(targetAttribute, dict[key]);
                }
            });
        });
    }
}

export { LanguageSwitcher };