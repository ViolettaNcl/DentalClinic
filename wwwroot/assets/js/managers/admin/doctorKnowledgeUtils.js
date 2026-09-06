// Pure helpers for the admin doctor/Denta knowledge editor.
// Kept browser-independent so validation can be covered by Node tests.

const LOCALIZED_NAME_FIELDS = ['fullNameEn', 'fullNameFr', 'fullNameEl', 'fullNameAr'];

export function buildDoctorPayload(values = {}, { edit = false } = {}) {
    const fullName = String(values.fullName || '').trim();
    if (!fullName) return { ok: false, error: 'Укажите ФИО врача' };
    if (fullName.length > 150) return { ok: false, error: 'ФИО врача слишком длинное' };

    const localizedNames = {};
    for (const field of LOCALIZED_NAME_FIELDS) {
        const value = String(values[field] || '').trim();
        if (value.length > 150)
            return { ok: false, error: 'Локализованное имя врача слишком длинное' };
        localizedNames[field] = value;
    }

    const specialization = String(values.specialization || '').trim();
    if (specialization.length > 300)
        return { ok: false, error: 'Специализация слишком длинная' };

    const bio = String(values.bio || '').trim();
    if (bio.length > 500)
        return { ok: false, error: 'Описание врача слишком длинное' };

    const rawExperience = String(values.experienceYears ?? '').trim();
    let experienceYears = null;
    if (rawExperience) {
        experienceYears = Number(rawExperience);
        if (!Number.isInteger(experienceYears) || experienceYears < 0 || experienceYears > 80)
            return { ok: false, error: 'Стаж должен быть целым числом от 0 до 80 лет' };
    }

    const payload = {
        fullName,
        ...localizedNames,
        specialization,
        experienceYears,
        bio,
    };

    if (edit) {
        payload.isActive = Boolean(values.isActive);
        payload.clearExperienceYears = experienceYears === null;
        if (experienceYears === null) delete payload.experienceYears;
    }

    return { ok: true, payload };
}

export function formatDoctorKnowledgeSummary(doctor = {}) {
    const specialization = String(doctor.specialization || '').trim();
    const experience = Number.isInteger(doctor.experienceYears) && doctor.experienceYears >= 0
        ? `стаж ${doctor.experienceYears} лет`
        : '';
    return [specialization, experience].filter(Boolean).join(' · ') || 'Профиль для Денты не заполнен';
}
