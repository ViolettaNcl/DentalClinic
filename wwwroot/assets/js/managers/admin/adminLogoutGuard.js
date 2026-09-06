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

// This module is imported by doctorsManager.js, one of the parser-inserted admin
// dashboard modules. Top-level await keeps DOMContentLoaded behind the cookie
// session check so adminDashboard.js sees restored metadata even in a new tab.
if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    try {
        bootstrappedAdminSession = await requireServerSession('admin');
        if (bootstrappedAdminSession) {
            const nameEl = document.querySelector('.panel-user-name');
            const emailEl = document.querySelector('.panel-user-email');
            if (nameEl) nameEl.textContent = bootstrappedAdminSession.name || 'Администратор';
            if (emailEl) emailEl.textContent = bootstrappedAdminSession.email || '—';
        }
    } catch (err) {
        console.error('Admin session bootstrap failed:', err?.message || err);
        const message = document.createElement('div');
        message.className = 'panel-error';
        message.style.margin = '16px';
        message.textContent = 'Не удалось проверить сеанс администратора. Обновите страницу или проверьте соединение.';
        document.body.prepend(message);
    }
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
