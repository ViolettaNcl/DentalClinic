export const CLINIC_KNOWLEDGE_LIMITS = Object.freeze({
    category: 80,
    title: 160,
    content: 1200,
    keywords: 300,
});

function normalize(value) {
    return String(value ?? '').trim();
}

export function buildClinicKnowledgePayload(values, { edit = false } = {}) {
    const category = normalize(values.category);
    const title = normalize(values.title);
    const content = normalize(values.content);
    const keywords = normalize(values.keywords);
    const sortText = normalize(values.sortOrder || '0');

    if (!category || !title || !content) {
        return { ok: false, error: 'Заполните категорию, заголовок и содержание.' };
    }

    for (const [field, max] of Object.entries(CLINIC_KNOWLEDGE_LIMITS)) {
        const value = field === 'category' ? category
            : field === 'title' ? title
                : field === 'content' ? content
                    : keywords;
        if (value.length > max) {
            return { ok: false, error: `Поле «${field}» длиннее допустимых ${max} символов.` };
        }
    }

    if (!/^\d+$/.test(sortText)) {
        return { ok: false, error: 'Порядок должен быть целым неотрицательным числом.' };
    }

    const sortOrder = Number(sortText);
    if (!Number.isSafeInteger(sortOrder) || sortOrder < 0 || sortOrder > 2147483647) {
        return { ok: false, error: 'Порядок вне допустимого диапазона.' };
    }

    const payload = {
        category,
        title,
        content,
        keywords: keywords || null,
        sortOrder,
        isActive: edit ? Boolean(values.isActive) : true,
    };

    return { ok: true, payload };
}
