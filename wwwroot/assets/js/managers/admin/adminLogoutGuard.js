import { apiFetch } from '../../services/apiClient.js';
import { showConfirm, showError } from '../../services/ui.js';
import { t } from '../../core/i18n.js';
import { terminateAdminSession } from '../../core/adminSession.js';

let installed = false;
let logoutInProgress = false;

export function installAdminLogoutGuard() {
    if (installed || typeof document === 'undefined') return;
    installed = true;

    // adminDashboard.js still has the legacy local-only logout listener. This
    // capture-phase guard runs before that bubble listener so an HttpOnly-cookie
    // session cannot survive after the UI claims that the admin logged out.
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
                clearSession: () => sessionStorage.clear(),
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
