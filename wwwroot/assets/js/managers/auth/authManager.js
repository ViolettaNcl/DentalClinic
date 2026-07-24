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

        this.setupEvents();
        this.updateHeader();

        // Кнопка "Выход" рисуется через innerHTML (не через data-i18n), поэтому
        // при смене языка её нужно перерисовать явно.
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
            if (Object.values(this.modals).includes(e.target)) {
                this.closeAll();
            }

            if (e.target.closest('.btn-logout')) {
                this.logout();
            }
        });

        Object.entries(this.modals).forEach(([key, modal]) => {
            modal?.querySelector('form')?.addEventListener('submit', e =>
                this.submitForm(e, key)
            );
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
                // Сначала пробуем как пациента; если не подошло — пробуем как администратора.
                // Так не нужно угадывать роль по email на фронте.
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
                sessionStorage.setItem('patientId', res.id);
                sessionStorage.setItem('patientName', res.name);
                sessionStorage.setItem('patientEmail', res.email);
                sessionStorage.setItem('userRole', res.role);
                if (res.token) {
                    sessionStorage.setItem('authToken', res.token);
                }

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

                if (res.role?.toLowerCase() === 'admin') {
                    window.location.href = '/pages/admin-dashboard.html';
                } else {
                    window.location.href = '/pages/patient-dashboard.html';
                }
            }
        } catch (err) {
            showError(err.message || t('auth_error_generic', 'Ошибка авторизации'));
        }
    }

    showModal(modal) {
        this.closeAll();

        if (modal) {
            modal.style.display = 'block';
        }
    }

    closeAll() {
        Object.values(this.modals).forEach(modal => {
            if (modal) {
                modal.style.display = 'none';
            }
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
        if (ok) {
            sessionStorage.clear();
            window.location.href = '/index.html';
        }
    }
}

export { AuthManager };