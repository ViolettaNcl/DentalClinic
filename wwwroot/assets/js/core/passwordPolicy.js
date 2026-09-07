export const PASSWORD_MIN_LENGTH = 8;

export function isStrongPassword(value) {
    const password = String(value ?? '');
    if (password.length < PASSWORD_MIN_LENGTH) return false;

    // Match the server-side PasswordPolicy semantics while remaining friendly to
    // non-Latin alphabets used by the clinic's multilingual UI.
    return /\p{Lu}/u.test(password)
        && /\p{Ll}/u.test(password)
        && /\p{Nd}/u.test(password)
        && /[^\p{L}\p{N}]/u.test(password);
}

const REQUIREMENTS = Object.freeze({
    ru: 'Пароль должен содержать минимум 8 символов, включая заглавную и строчную буквы, цифру и специальный символ.',
    en: 'Password must contain at least 8 characters, including an uppercase letter, a lowercase letter, a number, and a special character.',
    fr: 'Le mot de passe doit contenir au moins 8 caractères, avec une majuscule, une minuscule, un chiffre et un caractère spécial.',
    el: 'Ο κωδικός πρέπει να έχει τουλάχιστον 8 χαρακτήρες, με κεφαλαίο και πεζό γράμμα, αριθμό και ειδικό χαρακτήρα.',
    ar: 'يجب أن تتكون كلمة المرور من 8 أحرف على الأقل، وأن تتضمن حرفًا كبيرًا وحرفًا صغيرًا ورقمًا ورمزًا خاصًا.'
});

export function passwordRequirementsMessage(lang = 'ru') {
    const normalized = String(lang || 'ru').trim().toLowerCase().split('-')[0];
    return REQUIREMENTS[normalized] || REQUIREMENTS.ru;
}
