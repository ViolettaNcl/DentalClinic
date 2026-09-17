import { apiFetch } from '../../services/apiClient.js';
import { showConfirm, showError } from '../../services/ui.js';
import { t } from '../../core/i18n.js';
import { terminateAdminSession } from '../../core/adminSession.js';
import { requireServerSession, clearSessionMetadata } from '../../core/sessionBootstrap.js';
import { installAdminExportCookieGuard } from './adminExportGuard.js';
import { installAdminAppointmentRenderGuard } from './adminAppointmentRenderGuard.js';

let installed = false;
let logoutInProgress = false;
let bootstrappedAdminSession = null;
let adminSessionBootstrapPromise = null;

function showSessionBootstrapError() {
    if (typeof document === 'undefined') return;
    if (document.querySelector('[data-admin-session-bootstrap-error]')) return;

    const message = document.createElement('div');
    message.className = 'panel-error';
    message.dataset.adminSessionBootstrapError = 'true';
    message.style.margin = '16px';
    message.textContent = 'Не удалось проверить сеанс администратора. Данные могут загружаться медленно; попробуйте обновить страницу или проверьте соединение.';
    document.body.prepend(message);
}

// Session verification can touch a remote SQL Server. Start it in the background,
// but never hold the ES-module graph (and therefore DOMContentLoaded/tab handlers)
// behind that network request. Invalid/missing sessions are still redirected by
// requireServerSession(), so this changes responsiveness, not the security boundary.
export function bootstrapAdminSession() {
    if (adminSessionBootstrapPromise) return adminSessionBootstrapPromise;
    if (typeof window === 'undefined' || typeof document === 'undefined')
        return Promise.resolve(null);

    adminSessionBootstrapPromise = (async () => {
        try {
            // Verify the cookie session first. This primes the short-lived token-version
            // cache before the dashboard starts its other protected API requests, which
            // prevents an initial burst of duplicate remote-SQL auth checks.
            bootstrappedAdminSession = await requireServerSession('admin');
            if (!bootstrappedAdminSession) return null;

            const nameEl = document.querySelector('.panel-user-name');
            const emailEl = document.querySelector('.panel-user-email');
            if (nameEl) nameEl.textContent = bootstrappedAdminSession.name || 'Администратор';
            if (emailEl) emailEl.textContent = bootstrappedAdminSession.email || '—';

            // Access management is browser-only and may perform its own protected API
            // call. Load it only after the session check above has completed.
            await import('./adminAccessManager.js');
            return bootstrappedAdminSession;
        } catch (err) {
            console.error('Admin session bootstrap failed:', err?.message || err);
            showSessionBootstrapError();
            return null;
        }
    })();

    return adminSessionBootstrapPromise;
}

export function getBootstrappedAdminSession() {
    return bootstrappedAdminSession;
}

export function installAdminLogoutGuard() {
    if (installed || typeof document === 'undefined') return;
    installed = true;
    installAdminExportCookieGuard();
    installAdminAppointmentRenderGuard();

    document.addEventListener('click', async event => {
        const button = event.target?.closest?.('#btn-logout');
        if (!button) return;

        event.preventDefault();
        event.stopImmediatePropagation();
        if (logoutInProgress) return;

        const ok = await showConfirm(
            t('auth_logout_confirm_admin_text', 'Вы уверены, что хотите выйти из панели администратора?'),
            {
                title: t('auth_logout_confirm_title', 'Выход из аккаунта'),
                confirmText: t('auth_logout_confirm_yes', 'Да, выйти'),
                cancelText: t('auth_logout_confirm_stay', 'Остаться'),
                danger: true,
                icon: '🚪'
            }
        );
        if (!ok) return;

        logoutInProgress = true;
        button.disabled = true;

        try {
            await terminateAdminSession({
                requestLogout: () => apiFetch('/auth/logout', { method: 'POST' }),
                clearSession: () => clearSessionMetadata(),
                redirect: url => window.location.replace(url)
            });
        } catch (err) {
            console.error('Admin logout failed:', err);
            logoutInProgress = false;
            button.disabled = false;
            showError('Не удалось завершить серверную сессию. Проверьте соединение и попробуйте ещё раз.');
        }
    }, true);
}

// Fire-and-forget on browser pages: this deliberately does not use top-level await.
if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    void bootstrapAdminSession();
}
