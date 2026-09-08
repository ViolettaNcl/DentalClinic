import { localizedFeedback } from './localizedFeedback.js';

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

export function passwordRequirementsMessage(lang = 'ru') {
    return localizedFeedback('passwordRequirements', lang);
}
