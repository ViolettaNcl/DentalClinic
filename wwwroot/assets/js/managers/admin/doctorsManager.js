import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, escapeHtml } from '../../services/ui.js';
import { installDoctorCalendarAvailability } from './doctorCalendarAvailability.js';
import { installAdminLogoutGuard } from './adminLogoutGuard.js';
import { installAdminAnalyticsSummary } from './adminAnalyticsSummary.js';
import { installServiceKnowledgeManager } from './serviceKnowledgeManager.js';
import { buildDoctorPayload, formatDoctorKnowledgeSummary } from './doctorKnowledgeUtils.js';

installAdminLogoutGuard();
installAdminAnalyticsSummary();
installServiceKnowledgeManager();

class DoctorsManager {
    constructor() {
        this.tbody = document.getElementById('admin-doctors-body');
        this.addBtn = document.getElementById('btn-add-doctor');

        this.modal = {
            wrap: document.getElementById('doctor-modal'),
            title: document.getElementById('doctor-modal-title'),
            form: document.getElementById('doctor-form'),
            id: document.getElementById('doctor-id'),
            fullName: document.getElementById('doctor-fullname'),
            activeGroup: document.getElementById('doctor-active-group'),
            active: document.getElementById('doctor-active'),
            close: document.getElementById('doctor-modal-close'),
            cancel: document.getElementById('doctor-modal-cancel'),
        };
    }

    init() {
        if (!this.tbody) return;

        this._ensureKnowledgeFields();
        this.addBtn?.addEventListener('click', () => this._openModal());
        this.modal.close?.addEventListener('click', () => this._hideModal());
        this.modal.cancel?.addEventListener('click', () => this._hideModal());
        this.modal.form?.addEventListener('submit', e => this._submit(e));

        this.loadAll();
    }

    _ensureKnowledgeFields() {
        if (!this.modal.form) return;
        if (!document.getElementById('doctor-specialization')) {
            this.modal.activeGroup?.insertAdjacentHTML('beforebegin', `
                <div class="panel-form-group doctor-knowledge-hint">
                    <small>Эти данные используются на сайте и в базе знаний Денты. Заполняйте только подтверждённую информацию о враче.</small>
                </div>
                <div class="panel-form-group">
                    <label for="doctor-specialization">Специализация</label>
                    <input type="text" id="doctor-specialization" maxlength="300" placeholder="Например: имплантология, хирургия">
                </div>
                <div class="panel-form-group">
                    <label for="doctor-experience">Стаж, лет</label>
                    <input type="number" id="doctor-experience" min="0" max="80" step="1" placeholder="Например: 12">
                </div>
                <div class="panel-form-group">
                    <label for="doctor-bio">Краткое описание для сайта и Денты</label>
                    <textarea id="doctor-bio" rows="4" maxlength="500" placeholder="Опыт, направления работы, профессиональный профиль"></textarea>
                </div>
            `);
        }

        this.modal.specialization = document.getElementById('doctor-specialization');
        this.modal.experienceYears = document.getElementById('doctor-experience');
        this.modal.bio = document.getElementById('doctor-bio');
        this.modal.fullName?.setAttribute('maxlength', '150');
    }

    async loadAll() {
        this.tbody.innerHTML = `<tr><td colspan="4">Загрузка...</td></tr>`;
        try {
            const doctors = await apiFetch('/doctor/admin/all');
            this._render(doctors);
        } catch (err) {
            console.error('DoctorsManager loadAll error:', err);
            this.tbody.innerHTML = `<tr><td colspan="4" class="panel-error">Ошибка загрузки</td></tr>`;
            showError('Не удалось загрузить список врачей');
        }
    }

    _render(doctors) {
        if (!doctors.length) {
            this.tbody.innerHTML = `<tr><td colspan="4" class="panel-empty">Врачей пока нет</td></tr>`;
            return;
        }

        this.tbody.innerHTML = doctors.map(d => `
            <tr data-id="${d.id}">
                <td>${d.id}</td>
                <td>
                    <strong>${escapeHtml(d.fullName)}</strong>
                    <div class="doctor-knowledge-summary">${escapeHtml(formatDoctorKnowledgeSummary(d))}</div>
                </td>
                <td>
                    <span class="status-badge ${d.isActive ? 'status-confirmed' : 'status-cancelled'}">
                        ${d.isActive ? 'Активен' : 'Отключён'}
                    </span>
                </td>
                <td class="col-actions">
                    <div class="panel-table-actions">
                        <button class="btn-tag btn-edit" data-action="edit" title="Редактировать профиль врача и данные Денты">✏️</button>
                        <button class="btn-tag ${d.isActive ? 'btn-cancel' : 'btn-confirm'}" data-action="toggle"
                                title="${d.isActive ? 'Деактивировать' : 'Активировать'}">
                            ${d.isActive ? '✕' : '✓'}
                        </button>
                    </div>
                </td>
            </tr>
        `).join('');

        this._injectStyles();
        this._cache = Object.fromEntries(doctors.map(d => [d.id, d]));

        this.tbody.querySelectorAll('[data-action="edit"]').forEach(btn => {
            const id = Number(btn.closest('tr').dataset.id);
            btn.addEventListener('click', () => this._openModal(this._cache[id]));
        });
        this.tbody.querySelectorAll('[data-action="toggle"]').forEach(btn => {
            const id = Number(btn.closest('tr').dataset.id);
            btn.addEventListener('click', () => this._toggleActive(this._cache[id]));
        });
    }

    _openModal(doctor = null) {
        this.modal.form?.reset();
        this.modal.id.value = doctor?.id || '';
        this.modal.fullName.value = doctor?.fullName || '';
        this.modal.specialization.value = doctor?.specialization || '';
        this.modal.experienceYears.value = doctor?.experienceYears ?? '';
        this.modal.bio.value = doctor?.bio || '';

        if (doctor) {
            this.modal.title.textContent = 'Редактировать врача / базу Денты';
            this.modal.activeGroup.style.display = '';
            this.modal.active.checked = !!doctor.isActive;
        } else {
            this.modal.title.textContent = 'Добавить врача';
            this.modal.activeGroup.style.display = 'none';
            this.modal.active.checked = true;
        }

        this.modal.wrap?.classList.remove('hidden');
    }

    _hideModal() {
        this.modal.wrap?.classList.add('hidden');
    }

    async _submit(e) {
        e.preventDefault();

        const id = this.modal.id.value;
        const result = buildDoctorPayload({
            fullName: this.modal.fullName.value,
            specialization: this.modal.specialization.value,
            experienceYears: this.modal.experienceYears.value,
            bio: this.modal.bio.value,
            isActive: this.modal.active.checked,
        }, { edit: Boolean(id) });

        if (!result.ok) {
            showError(result.error);
            return;
        }

        const submit = this.modal.form.querySelector('[type="submit"]');
        submit.disabled = true;
        try {
            if (id) {
                await apiFetch(`/doctor/${id}`, {
                    method: 'PUT',
                    body: JSON.stringify(result.payload)
                });
                showSuccess('Профиль врача и база Денты обновлены');
            } else {
                await apiFetch('/doctor', {
                    method: 'POST',
                    body: JSON.stringify(result.payload)
                });
                showSuccess('Врач добавлен в список и базу Денты');
            }

            this._hideModal();
            await this.loadAll();
            // Обновляем выпадающие списки врачей в других разделах панели
            // (запись по телефону, календарь, редактирование заявки).
            window.reloadDoctorSelects?.();
        } catch (err) {
            showError(err.message || 'Не удалось сохранить врача');
        } finally {
            submit.disabled = false;
        }
    }

    async _toggleActive(doctor) {
        if (!doctor) return;
        const willActivate = !doctor.isActive;

        if (!confirm(willActivate
            ? `Снова показывать врача "${doctor.fullName}" в записи, на сайте и в базе Денты?`
            : `Скрыть врача "${doctor.fullName}" из записи, сайта и базы Денты? История его приёмов сохранится.`)) {
            return;
        }

        try {
            await apiFetch(`/doctor/${doctor.id}`, {
                method: 'PUT',
                body: JSON.stringify({ isActive: willActivate })
            });
            showSuccess(willActivate ? 'Врач снова активен' : 'Врач деактивирован');
            await this.loadAll();
            window.reloadDoctorSelects?.();
        } catch (err) {
            showError(err.message || 'Не удалось изменить статус врача');
        }
    }

    _injectStyles() {
        if (document.getElementById('doctor-knowledge-styles')) return;
        const style = document.createElement('style');
        style.id = 'doctor-knowledge-styles';
        style.textContent = `
            .doctor-knowledge-summary{margin-top:4px;color:#687a75;font-size:.82rem;line-height:1.35}
            .doctor-knowledge-hint{padding:9px 11px;border-left:3px solid #13b39b;background:#f5fbf9;color:#51645f}
        `;
        document.head.appendChild(style);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const manager = new DoctorsManager();
    manager.init();
    window.DoctorsManagerInstance = manager;
    installDoctorCalendarAvailability();
});

export { DoctorsManager };
