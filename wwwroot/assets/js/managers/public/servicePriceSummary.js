// Pure helpers for rendering live service pricing on the public catalogue.
// No DOM/browser dependencies: these functions are covered by Node tests.

const LOCALES = {
    ru: 'ru-RU',
    en: 'en-US',
    fr: 'fr-FR',
    el: 'el-GR',
    ar: 'ar',
};

const FROM_LABELS = {
    ru: 'от',
    en: 'from',
    fr: 'à partir de',
    el: 'από',
    ar: 'ابتداءً من',
};

export function buildStartingPricesByPage(services = []) {
    const result = {};

    for (const service of Array.isArray(services) ? services : []) {
        if (!service || service.isActive === false) continue;

        const pageUrl = String(service.pageUrl || '').trim();
        if (!pageUrl.startsWith('/pages/') || pageUrl.includes('..') || pageUrl.includes('\\') || pageUrl.includes('//'))
            continue;

        const price = Number(service.priceFrom);
        if (!Number.isFinite(price) || price < 0) continue;

        if (!(pageUrl in result) || price < result[pageUrl]) {
            result[pageUrl] = price;
        }
    }

    return result;
}

export function formatStartingPrice(amount, lang = 'ru') {
    const price = Number(amount);
    if (!Number.isFinite(price) || price < 0) return '';

    const normalizedLang = Object.hasOwn(LOCALES, lang) ? lang : 'ru';
    const formatted = new Intl.NumberFormat(LOCALES[normalizedLang], {
        maximumFractionDigits: 2,
    }).format(price);

    return `${FROM_LABELS[normalizedLang]} ${formatted} ₽`;
}
