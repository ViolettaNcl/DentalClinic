import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, showConfirm, queueToast } from '../../services/ui.js';
import { t, onLanguageChange } from '../../core/i18n.js';

class AuthManager {
    constructor() {
        this.modals = {};
    }

    async init() {
        this.modals.login = document.getElementById('login-modal');
        this.modals.signup = document.getElementById('signup-modal');

        if (!this.modals.login || !this.modals.signup) {
            console.warn('Модальные окна авторизации не найдены на странице');
            return;
        }

        // Stage 2 migration: access JWT now lives only in an HttpOnly cookie.
        sessionStorage.removeItem('authToken');

        this.setupEvents();
        this.updateHeader();
        onLanguageChange(() => this.updateHeader());
    }

    setupEvents() {
        const modalBtns = [
            ['.btn-login', 'login'],
            ['.btn-signup', 'signup'],
            ['.link-to-login', 'login'],
            ['.link-to-signup', 'signup']
        ];

        modalBtns.forEach(([selector, key]) => {
            document.querySelector(selector)?.addEventListener('click', e => {
                e.preventDefault();
                this.showModal(this.modals[key]);
            });
        });

        document.querySelectorAll('.close').forEach(btn =>
            btn.addEventListener('click', () => this.closeAll())
        );

        document.addEventListener('click', e => {
            if (Object.values(this.modals).includes(e.target)) this.closeAll();
            if (e.target.closest('.btn-logout')) this.logout();
        });

        Object.entries(this.modals).forEach(([key, modal]) => {
            modal?.querySelector('form')?.addEventListener('submit', e => this.submitForm(e, key));
        });
    }

    async submitForm(e, type) {
        e.preventDefault();
        const inputs = Array.from(e.target.querySelectorAll('input'));

        if (!inputs.every(i => i.value.trim())) {
            showError(t('auth_fill_all_fields', 'Заполните все поля!'));
            return;
        }

        const data = type === 'signup'
            ? {
                firstName: inputs[0].value.trim(),
                email: inputs[1].value.trim(),
                password: inputs[2].value.trim()
            }
            : {
                email: inputs[0].value.trim(),
                password: inputs[1].value.trim()
            };

        try {
            let res;
            if (type === 'signup') {
                res = await apiFetch('/auth/register', {
                    method: 'POST',
                    body: JSON.stringify(data)
                });
            } else {
                try {
                    res = await apiFetch('/auth/login', {
                        method: 'POST',
                        body: JSON.stringify(data)
                    });
                } catch (patientErr) {
                    res = await apiFetch('/auth/admin/login', {
                        method: 'POST',
                        body: JSON.stringify(data)
                    });
                }
            }

            if (res.id) {
                // Non-secret display/session metadata only. The signed JWT is set by the
                // server as HttpOnly and cannot be read by JavaScript.
                sessionStorage.setItem('patientId', res.id);
                sessionStorage.setItem('patientName', res.name);
                sessionStorage.setItem('patientEmail', res.email);
                sessionStorage.setItem('userRole', res.role);
                sessionStorage.removeItem('authToken');

                e.target.reset();
                this.closeAll();

                const isAdmin = res.role?.toLowerCase() === 'admin';
                const displayName = isAdmin ? t('auth_role_admin', 'Администратор') : res.name;
                const isNewAccount = type === 'signup';

                const celebrateMessage = isNewAccount
                    ? t('auth_signup_success', 'Регистрация прошла успешно{name}!').replace('{name}', displayName ? ', ' + displayName : '')
                    : t('auth_login_success', 'Рады видеть вас снова{name}!').replace('{name}', displayName ? ', ' + displayName : '');

                queueToast(celebrateMessage, 'success', {
                    celebrate: true,
                    icon: isNewAccount ? '🎉' : '👋',
                    title: isNewAccount ? t('auth_welcome_title', 'Добро пожаловать!') : t('auth_welcome_back_title', 'С возвращением!')
                });

                window.location.href = isAdmin
                    ? '/pages/admin-dashboard.html'
                    : '/pages/patient-dashboard.html';
            }
        } catch (err) {
            showError(err.message || t('auth_error_generic', 'Ошибка авторизации'));
        }
    }

    showModal(modal) {
        this.closeAll();
        if (modal) modal.style.display = 'block';
    }

    closeAll() {
        Object.values(this.modals).forEach(modal => {
            if (modal) modal.style.display = 'none';
        });
    }

    updateHeader() {
        const name = sessionStorage.getItem('patientName');
        const cabinet = document.getElementById('cabinet-link');
        const headerBtns = document.getElementById('header-buttons');

        if (name && cabinet && headerBtns) {
            cabinet.style.display = 'block';
            headerBtns.innerHTML =
                `<button class="btn-primary btn-logout">${t('auth_logout_btn', 'Выход')}</button>`;
        }
    }

    async logout() {
        const ok = await showConfirm(t('auth_logout_confirm_text', 'Вы уверены, что хотите выйти из личного кабинета?'), {
            title: t('auth_logout_confirm_title', 'Выход из аккаунта'),
            confirmText: t('auth_logout_confirm_yes', 'Да, выйти'),
            cancelText: t('auth_logout_confirm_stay', 'Остаться'),
            danger: true,
            icon: '🚪'
        });
        if (!ok) return;

        try {
            await apiFetch('/auth/logout', { method: 'POST' });
        } catch {
            // Local session metadata still has to be cleared even if the network is down;
            // the HttpOnly cookie will expire server-side/on its configured expiry.
        }

        ['patientId', 'patientName', 'patientEmail', 'userRole', 'authToken']
            .forEach(key => sessionStorage.removeItem(key));
        window.location.href = '/index.html';
    }
}

export { AuthManager };
