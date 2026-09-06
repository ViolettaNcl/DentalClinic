const NAME_FIELDS = Object.freeze(['fullName', 'fullNameEn', 'fullNameFr', 'fullNameEl', 'fullNameAr']);

const EXPERIENCE_LABELS = Object.freeze({
    ru: 'лет опыта',
    en: 'years of experience',
    fr: "ans d'expérience",
    el: 'χρόνια εμπειρίας',
    ar: 'سنوات من الخبرة',
});

const BOOK_LABELS = Object.freeze({
    ru: 'Записаться на приём',
    en: 'Book an appointment',
    fr: 'Prendre rendez-vous',
    el: 'Κλείστε ραντεβού',
    ar: 'احجز موعدًا',
});

export function localizedDoctorName(doctor = {}, language = 'ru') {
    const byLanguage = {
        en: doctor.fullNameEn,
        fr: doctor.fullNameFr,
        el: doctor.fullNameEl,
        ar: doctor.fullNameAr,
        ru: doctor.fullName,
    };
    return String(byLanguage[language] || doctor.fullName || doctor.fullNameEn || '').trim();
}

export function normalizeDoctorName(value) {
    return String(value || '')
        .normalize('NFKC')
        .toLocaleLowerCase()
        .replace(/^(dr\.?|д-?р\.?|доктор|δρ\.?|د\.?|دكتور)\s+/iu, '')
        .replace(/[\p{P}\p{S}]+/gu, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}

export function findDoctorByRenderedName(renderedName, doctors = []) {
    const wanted = normalizeDoctorName(renderedName);
    if (!wanted) return null;

    return doctors.find(doctor => NAME_FIELDS.some(field =>
        normalizeDoctorName(doctor?.[field]) === wanted
    )) || null;
}

export function doctorExperienceText(doctor = {}, language = 'ru') {
    const years = Number(doctor.experienceYears);
    if (!Number.isInteger(years) || years < 0) return '';
    const label = EXPERIENCE_LABELS[language] || EXPERIENCE_LABELS.ru;
    return `${years}+ ${label}`;
}

export function bookingLabel(language = 'ru') {
    return BOOK_LABELS[language] || BOOK_LABELS.ru;
}

export function doctorInitials(doctor = {}, language = 'ru') {
    const name = localizedDoctorName(doctor, language);
    return name
        .split(/\s+/)
        .map(part => part.replace(/[^\p{L}\p{N}]/gu, ''))
        .filter(Boolean)
        .slice(0, 2)
        .map(part => part[0]?.toUpperCase() || '')
        .join('') || 'DC';
}
