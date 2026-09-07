import { showError } from '../../services/ui.js';
import { getLang } from '../../core/i18n.js';
import {
    isStrongPassword,
    passwordRequirementsMessage,
    PASSWORD_MIN_LENGTH
} from '../../core/passwordPolicy.js';

export function installPatientPasswordPolicyGuard() {
    const form = document.getElementById('password-form');
    const password = document.getElementById('profile-new-password');
    if (!form || !password || form.dataset.strongPasswordGuard === '1') return;

    form.dataset.strongPasswordGuard = '1';
    password.minLength = PASSWORD_MIN_LENGTH;
    password.autocomplete = 'new-password';
    password.title = passwordRequirementsMessage(getLang());

    // Capture before the legacy dashboard submit listener. The server remains the
    // source of truth; this guard prevents an avoidable round-trip and replaces the
    // stale six-character browser check with the same effective policy.
    form.addEventListener('submit', event => {
        if (isStrongPassword(password.value)) return;

        event.preventDefault();
        event.stopImmediatePropagation();
        password.title = passwordRequirementsMessage(getLang());
        showError(password.title);
        password.focus();
    }, true);
}
