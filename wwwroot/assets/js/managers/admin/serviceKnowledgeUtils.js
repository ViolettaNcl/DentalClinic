// Pure service-catalogue helpers. This module intentionally has no DOM/browser
// dependencies so validation/formatting can be unit-tested directly in Node.

export const SERVICE_FIELD_LIMITS = Object.freeze({
    category: 100,
    name: 200,
    description: 500,
    unit: 30,
    keywords: 300,
    pageUrl: 300,
});

export const SERVICE_MAX_PRICE = 99_999_999.99;

export function formatServicePrice(service = {}) {
    const from = Number(service.priceFrom ?? 0);
    const to = service.priceTo === null || service.priceTo === undefined || service.priceTo === ''
        ? null
        : Number(service.priceTo);
    const unit = String(service.unit || '').trim();
    const range = to !== null && Number.isFinite(to) && to !== from
        ? `${formatNumber(from)}–${formatNumber(to)} ₽`
        : `${formatNumber(from)} ₽`;
    return unit ? `${range} / ${unit}` : range;
}

export function buildServicePayload(values = {}, { edit = false } = {}) {
    const category = String(values.category || '').trim();
    const name = String(values.name || '').trim();
    const description = String(values.description || '').trim();
    const unit = String(values.unit || '').trim();
    const keywords = String(values.keywords || '').trim();
    const pageUrl = String(values.pageUrl || '').trim();

    if (!category || !name) return { ok: false, error: 'Укажите категорию и название услуги' };

    const lengthError = firstLengthError({ category, name, description, unit, keywords, pageUrl });
    if (lengthError) return { ok: false, error: lengthError };

    const priceFrom = parseMoney(values.priceFrom);
    if (priceFrom === null || priceFrom < 0 || priceFrom > SERVICE_MAX_PRICE)
        return { ok: false, error: 'Цена «от» должна быть от 0 до 99 999 999,99 и содержать не более 2 знаков после запятой' };

    const rawTo = String(values.priceTo ?? '').trim();
    const priceTo = rawTo ? parseMoney(rawTo) : null;
    if (rawTo && (priceTo === null || priceTo < priceFrom || priceTo > SERVICE_MAX_PRICE))
        return { ok: false, error: 'Цена «до» должна быть не ниже цены «от», не выше 99 999 999,99 и содержать не более 2 знаков после запятой' };

    if (pageUrl && (!pageUrl.startsWith('/pages/') || pageUrl.includes('..') || pageUrl.includes('\\') || pageUrl.includes('//')))
        return { ok: false, error: 'Ссылка должна вести на локальную страницу /pages/...' };

    const sortOrderRaw = String(values.sortOrder ?? '').trim();
    const sortOrder = sortOrderRaw ? Number(sortOrderRaw) : 0;
    if (!Number.isInteger(sortOrder) || sortOrder < 0)
        return { ok: false, error: 'Порядок сортировки должен быть целым числом 0 или больше' };

    const payload = {
        category,
        name,
        description,
        priceFrom,
        priceTo,
        unit,
        keywords,
        pageUrl,
        sortOrder,
    };

    if (edit) {
        payload.clearPriceTo = !rawTo;
        payload.isActive = Boolean(values.isActive);
        if (!rawTo) delete payload.priceTo;
    }

    return { ok: true, payload };
}

function firstLengthError(values) {
    const labels = {
        category: 'Категория',
        name: 'Название',
        description: 'Описание',
        unit: 'Единица',
        keywords: 'Ключевые слова',
        pageUrl: 'Локальная страница',
    };

    for (const [field, max] of Object.entries(SERVICE_FIELD_LIMITS)) {
        if (String(values[field] || '').length > max)
            return `${labels[field]}: максимум ${max} символов`;
    }
    return null;
}

function parseMoney(value) {
    const normalized = String(value ?? '').trim().replace(',', '.');
    if (!/^\d+(?:\.\d{1,2})?$/.test(normalized)) return null;
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
}

function formatNumber(value) {
    return Number(value || 0).toLocaleString('ru-RU', { maximumFractionDigits: 2 });
}
