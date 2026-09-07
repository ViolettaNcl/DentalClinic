import { requireServerSession } from '../../core/sessionBootstrap.js';
import { escapeHtmlAttribute } from '../../services/htmlAttributeSafety.js';
import { installPatientPasswordPolicyGuard } from './patientPasswordPolicyGuard.js';

try {
    const session = await requireServerSession('patient');
    if (session) {
        // Install the password-policy capture guard before the legacy dashboard
        // module registers its submit listener. This keeps the browser aligned with
        // the server's shared strong-password policy without changing login behavior.
        installPatientPasswordPolicyGuard();

        // Import the dashboard modules only after the HttpOnly-cookie session has
        // restored non-secret display metadata into this tab's sessionStorage.
        await import('./patientDashboard.js');
        const { MyReviewsManager } = await import('../public/myReviews.js');

        // MyReviewsManager also stores the original review text in a quoted data-*
        // attribute for the translation toggle. Patch its renderer after the module
        // is evaluated but before this top-level-await entry module releases
        // DOMContentLoaded, so quotes cannot break out of that attribute.
        MyReviewsManager.prototype._escape = escapeHtmlAttribute;
    }
} catch (err) {
    console.error('Patient session bootstrap failed:', err?.message || err);
    const message = document.createElement('div');
    message.className = 'panel-error';
    message.style.margin = '24px';
    message.textContent = 'Не удалось проверить сеанс. Обновите страницу или проверьте соединение.';
    document.body.prepend(message);
}
