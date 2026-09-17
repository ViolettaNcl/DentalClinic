// Pure helpers for the admin doctor/Denta knowledge editor.
// Kept browser-independent so validation can be covered by Node tests.

const LOCALIZED_NAME_FIELDS = ['fullNameEn', 'fullNameFr', 'fullNameEl', 'fullNameAr'];
const LIMITS = Object.freeze({
    specialization: 300,
    bio: 500,
    roleTitle: 300,
    education: 1200,
    skills: 1200,
    philosophy: 500,
    stat2Value: 40,
    stat2Label: 80,
    stat3Value: 40,
    stat3Label: 80,
});

function clean(value) {
    return String(value ?? '').trim();
}

function validateLength(field, value, label) {
    const max = LIMITS[field];
    if (max && value.length > max)
        return `${label} слишком длинное (максимум ${max} символов)`;
    return null;
}

export function buildDoctorPayload(values = {}, { edit = false } = {}) {
    const fullName = clean(values.fullName);
    if (!fullName) return { ok: false, error: 'Укажите ФИО врача' };
    if (fullName.length > 150) return { ok: false, error: 'ФИО врача слишком длинное' };

    const localizedNames = {};
    for (const field of LOCALIZED_NAME_FIELDS) {
        const value = clean(values[field]);
        if (value.length > 150)
            return { ok: false, error: 'Локализованное имя врача слишком длинное' };
        localizedNames[field] = value;
    }

    const richFields = {
        specialization: clean(values.specialization),
        bio: clean(values.bio),
    };
    for (const field of ['roleTitle', 'education', 'skills', 'philosophy', 'stat2Value', 'stat2Label', 'stat3Value', 'stat3Label']) {
        if (Object.prototype.hasOwnProperty.call(values, field)) richFields[field] = clean(values[field]);
    }

    const labels = {
        specialization: 'Специализация',
        bio: 'Описание врача',
        roleTitle: 'Профессиональный заголовок',
        education: 'Образование',
        skills: 'Направления работы',
        philosophy: 'Философия врача',
        stat2Value: 'Второй показатель',
        stat2Label: 'Подпись второго показателя',
        stat3Value: 'Третий показатель',
        stat3Label: 'Подпись третьего показателя',
    };

    for (const [field, value] of Object.entries(richFields)) {
        const error = validateLength(field, value, labels[field]);
        if (error) return { ok: false, error };
    }

    const rawExperience = clean(values.experienceYears);
    let experienceYears = null;
    if (rawExperience) {
        experienceYears = Number(rawExperience);
        if (!Number.isInteger(experienceYears) || experienceYears < 0 || experienceYears > 80)
            return { ok: false, error: 'Стаж должен быть целым числом от 0 до 80 лет' };
    }

    const payload = {
        fullName,
        ...localizedNames,
        ...richFields,
        experienceYears,
    };

    if (edit) {
        payload.isActive = Boolean(values.isActive);
        payload.clearExperienceYears = experienceYears === null;
        if (experienceYears === null) delete payload.experienceYears;
    }

    return { ok: true, payload };
}

export function formatDoctorKnowledgeSummary(doctor = {}) {
    const specialization = clean(doctor.specialization);
    const role = clean(doctor.roleTitle);
    const experience = Number.isInteger(doctor.experienceYears) && doctor.experienceYears >= 0
        ? `стаж ${doctor.experienceYears} лет`
        : '';
    const photo = doctor.photoUrl ? 'фото ✓' : '';
    return [role || specialization, experience, photo].filter(Boolean).join(' · ') || 'Профиль для Денты не заполнен';
}
