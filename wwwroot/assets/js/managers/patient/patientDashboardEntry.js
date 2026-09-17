import { requireServerSession } from '../../core/sessionBootstrap.js';
import { escapeHtmlAttribute } from '../../services/htmlAttributeSafety.js';
import { installPatientPasswordPolicyGuard } from './patientPasswordPolicyGuard.js';
import { TabManager } from '../../core/tabManager.js';
import { runWhenDomReady } from '../../core/domReady.js';
import { apiFetch } from '../../services/apiClient.js';
import { showConfirm, showError } from '../../services/ui.js';
import { t } from '../../core/i18n.js';
import { terminateCookieSession } from '../../core/sessionTermination.js';
import { clearSessionMetadata } from '../../core/sessionBootstrap.js';


function installPatientLogout() {
    const button = document.getElementById('btn-patient-logout');
    if (!button || button.dataset.logoutReady === 'true') return;
    button.dataset.logoutReady = 'true';

    button.addEventListener('click', async () => {
        const ok = await showConfirm(
            t('auth_logout_confirm_text', 'Вы уверены, что хотите выйти из личного кабинета?'),
            {
                title: t('auth_logout_confirm_title', 'Выход из аккаунта'),
                confirmText: t('auth_logout_confirm_yes', 'Да, выйти'),
                cancelText: t('auth_logout_confirm_stay', 'Остаться'),
                danger: true,
                icon: '🚪'
            }
        );
        if (!ok) return;

        button.disabled = true;
        try {
            await terminateCookieSession({
                requestLogout: () => apiFetch('/auth/logout', { method: 'POST' }),
                clearSession: () => clearSessionMetadata(),
                redirect: url => window.location.replace(url)
            });
        } catch (err) {
            button.disabled = false;
            console.error('Patient logout failed:', err?.message || err);
            showError(t('auth_logout_retry_error', 'Не удалось завершить сеанс. Проверьте соединение и попробуйте ещё раз.'));
        }
    });
}

// Install cabinet navigation as soon as DOMContentLoaded fires. The secure session
// check may wait on a remote SQL Server and must never block local tab switching.
runWhenDomReady(() => {
    installPatientLogout();
    if (window.__patientDashboardTabsInstalled) return;
    const tabManager = new TabManager({
        navSelector: '.panel-nav-link',
        sectionSelector: '.panel-section',
        defaultSection: 'active',
    });
    tabManager.init();
    window.__patientDashboardTabsInstalled = true;
});

async function bootstrapPatientDashboard() {
    try {
        const session = await requireServerSession('patient');
        if (!session) return;

        // Install the password-policy capture guard before the legacy dashboard
        // module registers its submit listener. This keeps the browser aligned with
        // the server's shared strong-password policy without changing login behavior.
        installPatientPasswordPolicyGuard();

        // Import data-heavy dashboard modules only after the HttpOnly-cookie session
        // has restored non-secret display metadata. The async bootstrap itself is
        // fire-and-forget, so it does not delay DOMContentLoaded or tab handlers.
        await import('./patientDashboard.js');
        const { MyReviewsManager } = await import('../public/myReviews.js');

        // MyReviewsManager stores original review text in a quoted data-* attribute.
        // Patch its renderer before users open the review tab.
        MyReviewsManager.prototype._escape = escapeHtmlAttribute;
    } catch (err) {
        console.error('Patient session bootstrap failed:', err?.message || err);
        if (document.querySelector('[data-patient-session-bootstrap-error]')) return;
        const message = document.createElement('div');
        message.className = 'panel-error';
        message.dataset.patientSessionBootstrapError = 'true';
        message.style.margin = '24px';
        message.textContent = 'Не удалось проверить сеанс. Данные могут загружаться медленно; обновите страницу или проверьте соединение.';
        document.body.prepend(message);
    }
}

// Intentionally no top-level await: keep the page interactive while auth/data loads.
void bootstrapPatientDashboard();
