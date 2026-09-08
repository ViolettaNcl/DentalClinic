import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError } from '../../services/ui.js';
import { getLang } from '../../core/i18n.js';
import { localizedFeedback } from '../../core/localizedFeedback.js';

const APPOINTMENT_MESSAGE_KEYS = Object.freeze({
    phoneRequired: 'appointmentPhoneRequired',
    success: 'appointmentSuccess',
    submitError: 'appointmentSubmitError'
});

function appointmentMessage(key) {
    return localizedFeedback(APPOINTMENT_MESSAGE_KEYS[key] ?? key, getLang());
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
