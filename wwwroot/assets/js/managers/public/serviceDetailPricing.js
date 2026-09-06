// Pure helpers for synchronizing service-detail price cards with the public DB catalogue.
// No DOM/browser dependencies: covered directly by Node tests.

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

function normalizeLocalServicePage(value) {
    const path = String(value || '').trim().split(/[?#]/, 1)[0];
    if (!path.startsWith('/pages/services/')) return '';
    if (path.includes('..') || path.includes('\\') || path.includes('//')) return '';
    return path;
}

export function buildDetailPriceSlots(services = [], pageUrl = '', maxSlots = Number.POSITIVE_INFINITY) {
    const page = normalizeLocalServicePage(pageUrl);
    const limit = Number.isFinite(Number(maxSlots)) ? Math.max(0, Math.trunc(Number(maxSlots))) : Number.POSITIVE_INFINITY;
    if (!page || limit === 0) return [];

    const slots = new Map();

    for (const service of Array.isArray(services) ? services : []) {
        if (!service || service.isActive === false) continue;
        if (normalizeLocalServicePage(service.pageUrl) !== page) continue;

        const priceFrom = Number(service.priceFrom);
        if (!Number.isFinite(priceFrom) || priceFrom < 0) continue;

        const sortOrder = Number(service.sortOrder);
        if (!Number.isInteger(sortOrder) || sortOrder < 1) continue;

        const slotIndex = sortOrder - 1;
        if (slotIndex >= limit || slots.has(slotIndex)) continue;

        slots.set(slotIndex, service);
    }

    return [...slots.entries()]
        .sort(([a], [b]) => a - b)
        .map(([slotIndex, service]) => ({ slotIndex, service }));
}

export function formatDetailServicePrice(service = {}, lang = 'ru') {
    const priceFrom = Number(service.priceFrom);
    if (!Number.isFinite(priceFrom) || priceFrom < 0) return '';

    const rawTo = service.priceTo;
    const priceTo = rawTo === null || rawTo === undefined || rawTo === '' ? null : Number(rawTo);
    if (priceTo !== null && (!Number.isFinite(priceTo) || priceTo < priceFrom)) return '';

    const normalizedLang = Object.hasOwn(LOCALES, lang) ? lang : 'ru';
    const formatter = new Intl.NumberFormat(LOCALES[normalizedLang], { maximumFractionDigits: 2 });
    const from = formatter.format(priceFrom);

    if (priceTo !== null && priceTo > priceFrom)
        return `${from}–${formatter.format(priceTo)} ₽`;

    if (priceTo === priceFrom)
        return `${from} ₽`;

    return `${FROM_LABELS[normalizedLang]} ${from} ₽`;
}
