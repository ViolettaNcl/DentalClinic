// Pure service-catalogue helpers. This module intentionally has no DOM/browser
// dependencies so validation/formatting can be unit-tested directly in Node.

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
    if (!category || !name) return { ok: false, error: 'Укажите категорию и название услуги' };

    const priceFrom = parseMoney(values.priceFrom);
    if (priceFrom === null || priceFrom < 0)
        return { ok: false, error: 'Укажите корректную цену «от»' };

    const rawTo = String(values.priceTo ?? '').trim();
    const priceTo = rawTo ? parseMoney(rawTo) : null;
    if (rawTo && (priceTo === null || priceTo < priceFrom))
        return { ok: false, error: 'Цена «до» не может быть ниже цены «от»' };

    const pageUrl = String(values.pageUrl || '').trim();
    if (pageUrl && (!pageUrl.startsWith('/pages/') || pageUrl.includes('..') || pageUrl.includes('\\') || pageUrl.includes('//')))
        return { ok: false, error: 'Ссылка должна вести на локальную страницу /pages/...' };

    const sortOrderRaw = String(values.sortOrder ?? '').trim();
    const sortOrder = sortOrderRaw ? Number.parseInt(sortOrderRaw, 10) : 0;
    if (!Number.isFinite(sortOrder)) return { ok: false, error: 'Некорректный порядок сортировки' };

    const payload = {
        category,
        name,
        description: String(values.description || '').trim(),
        priceFrom,
        priceTo,
        unit: String(values.unit || '').trim(),
        keywords: String(values.keywords || '').trim(),
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

function parseMoney(value) {
    const normalized = String(value ?? '').trim().replace(',', '.');
    if (!normalized) return null;
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
}

function formatNumber(value) {
    return Number(value || 0).toLocaleString('ru-RU', { maximumFractionDigits: 2 });
}
