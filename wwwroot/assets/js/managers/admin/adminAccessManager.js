import { apiFetch } from '../../services/apiClient.js';
import { showConfirm, showError, showSuccess } from '../../services/ui.js';

const SECTION_ID = 'section-admin-access';
const STORAGE_KEY = 'admin_active_section';

let currentAdminId = null;
let admins = [];

function recoverOrdinaryAdminNavigation() {
    if (sessionStorage.getItem(STORAGE_KEY) !== 'admin-access') return;
    sessionStorage.setItem(STORAGE_KEY, 'requests');
    document.querySelector('.panel-nav-link[data-section="requests"]')?.click();
}

function buildShell() {
    if (document.getElementById(SECTION_ID)) return;

    const nav = document.querySelector('.panel-nav');
    const profileLink = nav?.querySelector('[data-section="profile"]');
    if (!nav || !profileLink) return;

    const navButton = document.createElement('button');
    navButton.type = 'button';
    navButton.className = 'panel-nav-link';
    navButton.dataset.section = 'admin-access';
    navButton.textContent = 'Администраторы';
    profileLink.insertAdjacentElement('beforebegin', navButton);

    const content = document.querySelector('.panel-content-inner');
    if (!content) return;

    const section = document.createElement('section');
    section.className = 'panel-section hidden';
    section.id = SECTION_ID;
    section.innerHTML = `
        <div class="panel-section-header">
            <h2>Доступ администраторов</h2>
            <p>Создание администраторов и управление правами суперадминистратора.</p>
        </div>

        <div class="panel-card">
            <h3 class="analytics-chart-title">Добавить администратора</h3>
            <form id="admin-access-create-form" class="panel-form">
                <div class="panel-form-group">
                    <label for="admin-access-email">Email</label>
                    <input id="admin-access-email" type="email" maxlength="320" autocomplete="off" required>
                </div>
                <div class="panel-form-group">
                    <label for="admin-access-password">Временный пароль</label>
                    <input id="admin-access-password" type="password" minlength="8" maxlength="128" autocomplete="new-password" required>
                    <small>Минимум 8 символов: заглавная и строчная буквы, цифра и специальный символ.</small>
                </div>
                <div class="panel-form-group">
                    <label>
                        <input id="admin-access-super" type="checkbox">
                        Создать как суперадминистратора
                    </label>
                </div>
                <button type="submit" class="panel-btn-primary">Создать администратора</button>
            </form>
        </div>

        <div class="panel-card">
            <h3 class="analytics-chart-title">Учётные записи</h3>
            <div class="panel-table-wrap">
                <table class="panel-table">
                    <thead>
                        <tr>
                            <th>Email</th>
                            <th>Доступ</th>
                            <th>Создан</th>
                            <th class="col-actions">Действия</th>
                        </tr>
                    </thead>
                    <tbody id="admin-access-body"></tbody>
                </table>
            </div>
        </div>

        <div id="admin-access-password-panel" class="panel-card hidden">
            <h3 class="analytics-chart-title">Сбросить пароль</h3>
            <form id="admin-access-password-form" class="panel-form">
                <input id="admin-access-password-target" type="hidden">
                <p id="admin-access-password-target-label"></p>
                <div class="panel-form-group">
                    <label for="admin-access-new-password">Новый временный пароль</label>
                    <input id="admin-access-new-password" type="password" minlength="8" maxlength="128" autocomplete="new-password" required>
                </div>
                <div class="panel-card-toolbar">
                    <button type="submit" class="panel-btn-primary">Обновить пароль</button>
                    <button type="button" id="admin-access-password-cancel" class="panel-btn-secondary">Отмена</button>
                </div>
            </form>
        </div>
    `;
    content.appendChild(section);

    navButton.addEventListener('click', () => showSection());
    document.querySelectorAll('.panel-nav-link:not([data-section="admin-access"])').forEach(link => {
        link.addEventListener('click', () => section.classList.add('hidden'));
    });

    document.getElementById('admin-access-create-form')?.addEventListener('submit', createAdmin);
    document.getElementById('admin-access-password-form')?.addEventListener('submit', submitPasswordReset);
    document.getElementById('admin-access-password-cancel')?.addEventListener('click', closePasswordReset);
}

function showSection() {
    document.querySelectorAll('.panel-section').forEach(section => section.classList.add('hidden'));
    document.querySelectorAll('.panel-nav-link').forEach(link => link.classList.remove('active'));
    document.getElementById(SECTION_ID)?.classList.remove('hidden');
    document.querySelector('.panel-nav-link[data-section="admin-access"]')?.classList.add('active');
    sessionStorage.setItem(STORAGE_KEY, 'admin-access');
}

function appendCell(row, text) {
    const cell = document.createElement('td');
    cell.textContent = text;
    row.appendChild(cell);
    return cell;
}

function actionButton(text, handler, className = 'panel-btn-secondary') {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = className;
    button.textContent = text;
    button.addEventListener('click', handler);
    return button;
}

function render() {
    const body = document.getElementById('admin-access-body');
    if (!body) return;
    body.replaceChildren();

    currentAdminId = admins.find(admin => admin.isCurrent)?.id ?? null;

    for (const admin of admins) {
        const row = document.createElement('tr');
        appendCell(row, admin.isCurrent ? `${admin.email} (вы)` : admin.email);
        appendCell(row, admin.isSuperAdmin ? 'Суперадминистратор' : 'Администратор');
        appendCell(row, admin.createdAt ? new Date(admin.createdAt).toLocaleString('ru-RU') : '—');

        const actions = document.createElement('td');
        actions.className = 'col-actions';

        const toggleLabel = admin.isSuperAdmin ? 'Снять super' : 'Сделать super';
        actions.appendChild(actionButton(toggleLabel, () => toggleSuperAdmin(admin)));
        actions.appendChild(actionButton('Сбросить пароль', () => openPasswordReset(admin)));
        if (!admin.isCurrent)
            actions.appendChild(actionButton('Удалить', () => deleteAdmin(admin), 'panel-btn-danger'));

        row.appendChild(actions);
        body.appendChild(row);
    }

    if (!admins.length) {
        const row = document.createElement('tr');
        const cell = document.createElement('td');
        cell.colSpan = 4;
        cell.textContent = 'Администраторы не найдены';
        row.appendChild(cell);
        body.appendChild(row);
    }
}

async function loadAdmins() {
    admins = await apiFetch('/admin-access');
    render();
}

async function createAdmin(event) {
    event.preventDefault();
    const form = event.currentTarget;
    const email = document.getElementById('admin-access-email')?.value.trim();
    const password = document.getElementById('admin-access-password')?.value || '';
    const isSuperAdmin = Boolean(document.getElementById('admin-access-super')?.checked);

    try {
        await apiFetch('/admin-access', {
            method: 'POST',
            body: JSON.stringify({ email, password, isSuperAdmin })
        });
        form.reset();
        await loadAdmins();
        showSuccess('Администратор создан');
    } catch (error) {
        showError(error?.message || 'Не удалось создать администратора');
    }
}

async function toggleSuperAdmin(admin) {
    const nextValue = !admin.isSuperAdmin;
    const confirmed = await showConfirm(
        nextValue
            ? `Выдать права суперадминистратора для ${admin.email}?`
            : `Снять права суперадминистратора с ${admin.email}?`,
        {
            title: 'Изменение доступа',
            confirmText: nextValue ? 'Выдать права' : 'Снять права',
            cancelText: 'Отмена',
            danger: !nextValue
        }
    );
    if (!confirmed) return;

    try {
        await apiFetch(`/admin-access/${admin.id}/super-admin`, {
            method: 'PUT',
            body: JSON.stringify({ isSuperAdmin: nextValue })
        });

        if (admin.id === currentAdminId && !nextValue) {
            sessionStorage.clear();
            window.location.href = '/index.html';
            return;
        }

        await loadAdmins();
        showSuccess('Права администратора обновлены');
    } catch (error) {
        showError(error?.message || 'Не удалось изменить права администратора');
    }
}

function openPasswordReset(admin) {
    const panel = document.getElementById('admin-access-password-panel');
    const target = document.getElementById('admin-access-password-target');
    const label = document.getElementById('admin-access-password-target-label');
    const password = document.getElementById('admin-access-new-password');
    if (!panel || !target || !label || !password) return;

    target.value = String(admin.id);
    label.textContent = `Аккаунт: ${admin.email}`;
    password.value = '';
    panel.classList.remove('hidden');
    password.focus();
}

function closePasswordReset() {
    document.getElementById('admin-access-password-panel')?.classList.add('hidden');
    document.getElementById('admin-access-password-form')?.reset();
}

async function submitPasswordReset(event) {
    event.preventDefault();
    const targetId = Number(document.getElementById('admin-access-password-target')?.value);
    const newPassword = document.getElementById('admin-access-new-password')?.value || '';
    if (!Number.isInteger(targetId) || targetId <= 0) return;

    try {
        await apiFetch(`/admin-access/${targetId}/password`, {
            method: 'PUT',
            body: JSON.stringify({ newPassword })
        });
        closePasswordReset();
        showSuccess('Пароль администратора обновлён');

        if (targetId === currentAdminId) {
            sessionStorage.clear();
            window.location.href = '/index.html';
        }
    } catch (error) {
        showError(error?.message || 'Не удалось обновить пароль');
    }
}

async function deleteAdmin(admin) {
    const confirmed = await showConfirm(
        `Удалить администратора ${admin.email}? Его активные сессии перестанут работать сразу.`,
        {
            title: 'Удаление администратора',
            confirmText: 'Удалить',
            cancelText: 'Отмена',
            danger: true
        }
    );
    if (!confirmed) return;

    try {
        await apiFetch(`/admin-access/${admin.id}`, { method: 'DELETE' });
        await loadAdmins();
        showSuccess('Администратор удалён');
    } catch (error) {
        showError(error?.message || 'Не удалось удалить администратора');
    }
}

async function initAdminAccessManager() {
    try {
        admins = await apiFetch('/admin-access');
    } catch (error) {
        if (error?.status === 401 || error?.status === 403) {
            recoverOrdinaryAdminNavigation();
            return;
        }
        console.error('admin access bootstrap failed:', error?.message || error);
        recoverOrdinaryAdminNavigation();
        return;
    }

    buildShell();
    render();
    if (sessionStorage.getItem(STORAGE_KEY) === 'admin-access')
        showSection();
}

document.addEventListener('DOMContentLoaded', initAdminAccessManager);
