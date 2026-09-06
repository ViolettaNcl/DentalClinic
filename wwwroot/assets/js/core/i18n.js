// =====================================================
// 🌐 ОБЩИЙ ДВИЖОК ПЕРЕВОДА — используется как для статичного
// HTML (через data-i18n, см. languageSwitcher.js), так и для
// текста, который генерируется динамически из JS (тосты,
// confirm()-диалоги, подписи кнопок, содержимое таблиц и т.п.)
//
// Использование в любом другом JS-файле:
//   import { t, getLang, onLanguageChange } from '../core/i18n.js';
//   showError(t('err_generic'));
// =====================================================

import { applyTranslationQualityOverrides } from './translationQualityOverrides.js';

const DEFAULT_LANG = 'ru';
const STORAGE_KEY = 'site_lang';
const I18N_BASE_URL = '/assets/i18n/';

let currentLang = localStorage.getItem(STORAGE_KEY) || DEFAULT_LANG;
const cache = {};           // { ru: {...}, en: {...} }
const listeners = new Set(); // callbacks(lang, dict)

// Промис, который резолвится, когда словарь ТЕКУЩЕГО языка уже загружен —
// его стоит дождаться перед первым рендером динамических блоков
// (списки уведомлений, отзывов и т.д.), чтобы не показать секунду на русском,
// а потом резко перерисовать на выбранный язык.
let readyResolve;
const ready = new Promise(resolve => { readyResolve = resolve; });

async function loadDictionary(code) {
    if (cache[code]) return cache[code];
    try {
        const res = await fetch(`${I18N_BASE_URL}${code}.json`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = applyTranslationQualityOverrides(code, await res.json());
        cache[code] = data;
        return data;
    } catch (err) {
        console.error(`[i18n] Не удалось загрузить словарь "${code}":`, err);
        return null;
    }
}

// Загружаем словарь текущего языка сразу при импорте модуля —
// это самое первое, что должно произойти при загрузке страницы.
loadDictionary(currentLang).then(() => readyResolve());

function getLang() {
    return currentLang;
}

/**
 * Переключить язык, подгрузить словарь (если ещё не в кэше) и уведомить
 * всех подписчиков — они сами решают, что и как перерисовать.
 */
async function setLang(code) {
    currentLang = code;
    localStorage.setItem(STORAGE_KEY, code);
    const dict = await loadDictionary(code);
    listeners.forEach(cb => {
        try { cb(code, dict); } catch (err) { console.error('[i18n] listener error:', err); }
    });
    document.dispatchEvent(new CustomEvent('i18n:changed', { detail: { lang: code, dict } }));
    return dict;
}

/**
 * Перевести ключ на текущем языке. Если ключа нет в словаре — вернуть
 * fallback (если передан) или сам ключ, чтобы не падать в рантайме.
 */
function t(key, fallback) {
    const dict = cache[currentLang];
    if (dict && dict[key] !== undefined) return dict[key];
    if (fallback !== undefined) return fallback;
    return key;
}

/** Подписаться на смену языка. Возвращает функцию отписки. */
function onLanguageChange(cb) {
    listeners.add(cb);
    return () => listeners.delete(cb);
}

export { t, getLang, setLang, onLanguageChange, ready, DEFAULT_LANG };