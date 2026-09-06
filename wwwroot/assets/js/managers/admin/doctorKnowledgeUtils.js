// Pure helpers for the admin doctor/Denta knowledge editor.
// Kept browser-independent so validation can be covered by Node tests.

export function buildDoctorPayload(values = {}, { edit = false } = {}) {
    const fullName = String(values.fullName || '').trim();
    if (!fullName) return { ok: false, error: 'Укажите ФИО врача' };
    if (fullName.length > 150) return { ok: false, error: 'ФИО врача слишком длинное' };

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
        specialization,
        experienceYears,
        bio,
    };

    if (edit) payload.isActive = Boolean(values.isActive);
    return { ok: true, payload };
}

export function formatDoctorKnowledgeSummary(doctor = {}) {
    const specialization = String(doctor.specialization || '').trim();
    const experience = Number.isInteger(doctor.experienceYears) && doctor.experienceYears >= 0
        ? `стаж ${doctor.experienceYears} лет`
        : '';
    return [specialization, experience].filter(Boolean).join(' · ') || 'Профиль для Денты не заполнен';
}
