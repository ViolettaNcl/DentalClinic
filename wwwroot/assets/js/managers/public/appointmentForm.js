import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError } from '../../services/ui.js';
import { getLang } from '../../core/i18n.js';

const PUBLIC_APPOINTMENT_MESSAGES = Object.freeze({
    ru: Object.freeze({
        phoneRequired: 'Введите номер телефона!',
        success: 'Заявка отправлена! Скоро мы вам перезвоним.',
        submitError: 'Не удалось отправить заявку. Попробуйте ещё раз.'
    }),
    en: Object.freeze({
        phoneRequired: 'Enter your phone number.',
        success: "Request sent! We'll call you back soon.",
        submitError: 'Could not send the appointment request. Please try again.'
    }),
    fr: Object.freeze({
        phoneRequired: 'Veuillez saisir votre numéro de téléphone.',
        success: 'Demande envoyée ! Nous vous rappellerons bientôt.',
        submitError: "Impossible d’envoyer la demande de rendez-vous. Veuillez réessayer."
    }),
    el: Object.freeze({
        phoneRequired: 'Παρακαλώ εισαγάγετε τον αριθμό τηλεφώνου σας.',
        success: 'Το αίτημα στάλθηκε! Θα σας καλέσουμε σύντομα.',
        submitError: 'Δεν ήταν δυνατή η αποστολή του αιτήματος ραντεβού. Δοκιμάστε ξανά.'
    }),
    ar: Object.freeze({
        phoneRequired: 'يرجى إدخال رقم هاتفك.',
        success: 'تم إرسال الطلب! سنتصل بك قريبًا.',
        submitError: 'تعذر إرسال طلب الموعد. يرجى المحاولة مرة أخرى.'
    })
});

function appointmentMessage(key) {
    const lang = getLang();
    return PUBLIC_APPOINTMENT_MESSAGES[lang]?.[key]
        ?? PUBLIC_APPOINTMENT_MESSAGES.ru[key]
        ?? key;
}

class AppointmentForm {
    constructor() {
        this.patientId = sessionStorage.getItem('patientId');
        this.form = document.querySelector('.contact-form-block form');
        this.useProfilePhoneBtn = document.getElementById('use-profile-phone-btn');

        if (this.form) {
            this.form.addEventListener('submit', e => this.submit(e));
        }

        this._setupProfilePhoneHint();
    }

    // Если пациент авторизован и у него в профиле сохранён телефон — показываем
    // кнопку "подставить номер из профиля", чтобы не вводить его вручную каждый раз.
    async _setupProfilePhoneHint() {
        if (!this.patientId || !this.useProfilePhoneBtn) return;

        try {
            const profile = await apiFetch('/auth/profile');
            if (!profile.phone) return;

            this.useProfilePhoneBtn.classList.remove('hidden');
            this.useProfilePhoneBtn.addEventListener('click', () => {
                const phoneInput = document.getElementById('form-phone');
                if (phoneInput) phoneInput.value = profile.phone;
            });
        } catch (err) {
            console.error('Не удалось получить телефон из профиля:', err);
        }
    }

    async submit(e) {
        e.preventDefault();

        const formData = {
            PatientId: this.patientId ? parseInt(this.patientId) : null,
            FirstName:
                document.getElementById('form-name')?.value.trim() ||
                localStorage.getItem('patientName') ||
                '',
            Phone: document.getElementById('form-phone')?.value.trim(),
            Comment: document.getElementById('form-comment')?.value.trim() || '',
            AppointmentDate: document.getElementById('form-date')?.value
                ? document.getElementById('form-date').value + 'T00:00:00'
                : null
        };

        if (!formData.Phone) {
            showError(appointmentMessage('phoneRequired'));
            return;
        }

        try {
            await apiFetch('/appointmentrequest', {
                method: 'POST',
                body: JSON.stringify(formData)
            });

            showSuccess(appointmentMessage('success'));
            this.form.reset();

            if (this.patientId) {
                window.location.href = '/pages/patient-dashboard.html';
            }
        } catch (err) {
            console.error('Appointment request submission failed:', err);
            // API error text is not guaranteed to be localized and may contain
            // implementation details. Keep the public toast predictable and in the
            // currently selected UI language instead of surfacing raw server text.
            showError(appointmentMessage('submitError'));
        }
    }
}

document.addEventListener('DOMContentLoaded', () => new AppointmentForm());
