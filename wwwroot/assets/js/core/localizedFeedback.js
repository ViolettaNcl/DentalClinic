// Synchronous user-facing validation/feedback text shared by flows that can run
// before the async i18n dictionary has finished loading. Keep this small and
// regression-tested; normal UI copy should continue to use t(...) from i18n.js.
export const SUPPORTED_FEEDBACK_LANGUAGES = Object.freeze(['ru', 'en', 'fr', 'el', 'ar']);

const FEEDBACK = Object.freeze({
    ru: Object.freeze({
        passwordRequirements: 'Пароль должен содержать минимум 8 символов, включая заглавную и строчную буквы, цифру и специальный символ.',
        appointmentPhoneRequired: 'Введите номер телефона!',
        appointmentSuccess: 'Заявка отправлена! Скоро мы вам перезвоним.',
        appointmentSubmitError: 'Не удалось отправить заявку. Попробуйте ещё раз.'
    }),
    en: Object.freeze({
        passwordRequirements: 'Password must contain at least 8 characters, including an uppercase letter, a lowercase letter, a number, and a special character.',
        appointmentPhoneRequired: 'Enter your phone number.',
        appointmentSuccess: "Request sent! We'll call you back soon.",
        appointmentSubmitError: 'Could not send the appointment request. Please try again.'
    }),
    fr: Object.freeze({
        passwordRequirements: 'Le mot de passe doit contenir au moins 8 caractères, avec une majuscule, une minuscule, un chiffre et un caractère spécial.',
        appointmentPhoneRequired: 'Veuillez saisir votre numéro de téléphone.',
        appointmentSuccess: 'Demande envoyée ! Nous vous rappellerons bientôt.',
        appointmentSubmitError: 'Impossible d’envoyer la demande de rendez-vous. Veuillez réessayer.'
    }),
    el: Object.freeze({
        passwordRequirements: 'Ο κωδικός πρέπει να έχει τουλάχιστον 8 χαρακτήρες, με κεφαλαίο και πεζό γράμμα, αριθμό και ειδικό χαρακτήρα.',
        appointmentPhoneRequired: 'Παρακαλώ εισαγάγετε τον αριθμό τηλεφώνου σας.',
        appointmentSuccess: 'Το αίτημα στάλθηκε! Θα σας καλέσουμε σύντομα.',
        appointmentSubmitError: 'Δεν ήταν δυνατή η αποστολή του αιτήματος ραντεβού. Δοκιμάστε ξανά.'
    }),
    ar: Object.freeze({
        passwordRequirements: 'يجب أن تتكون كلمة المرور من 8 أحرف على الأقل، وأن تتضمن حرفًا كبيرًا وحرفًا صغيرًا ورقمًا ورمزًا خاصًا.',
        appointmentPhoneRequired: 'يرجى إدخال رقم هاتفك.',
        appointmentSuccess: 'تم إرسال الطلب! سنتصل بك قريبًا.',
        appointmentSubmitError: 'تعذر إرسال طلب الموعد. يرجى المحاولة مرة أخرى.'
    })
});

export function normalizeFeedbackLanguage(lang = 'ru') {
    const normalized = String(lang || 'ru').trim().toLowerCase().split('-')[0];
    return SUPPORTED_FEEDBACK_LANGUAGES.includes(normalized) ? normalized : 'ru';
}

export function localizedFeedback(key, lang = 'ru') {
    const normalized = normalizeFeedbackLanguage(lang);
    return FEEDBACK[normalized]?.[key] ?? FEEDBACK.ru[key] ?? key;
}

export function localizedFeedbackKeys() {
    return Object.keys(FEEDBACK.ru);
}
