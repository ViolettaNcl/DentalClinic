import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, escapeHtml } from '../../services/ui.js';

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

        this.addBtn?.addEventListener('click', () => this._openModal());
        this.modal.close?.addEventListener('click', () => this._hideModal());
        this.modal.cancel?.addEventListener('click', () => this._hideModal());
        this.modal.form?.addEventListener('submit', e => this._submit(e));

        this.loadAll();
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
                <td>${escapeHtml(d.fullName)}</td>
                <td>
                    <span class="status-badge ${d.isActive ? 'status-confirmed' : 'status-cancelled'}">
                        ${d.isActive ? 'Активен' : 'Отключён'}
                    </span>
                </td>
                <td class="col-actions">
                    <div class="panel-table-actions">
                        <button class="btn-tag btn-edit" data-action="edit" title="Редактировать">✏️</button>
                        <button class="btn-tag ${d.isActive ? 'btn-cancel' : 'btn-confirm'}" data-action="toggle"
                                title="${d.isActive ? 'Деактивировать' : 'Активировать'}">
                            ${d.isActive ? '✕' : '✓'}
                        </button>
                    </div>
                </td>
            </tr>
        `).join('');

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
        this.modal.id.value = doctor?.id || '';
        this.modal.fullName.value = doctor?.fullName || '';

        if (doctor) {
            this.modal.title.textContent = 'Редактировать врача';
            this.modal.activeGroup.style.display = '';
            this.modal.active.checked = !!doctor.isActive;
        } else {
            this.modal.title.textContent = 'Добавить врача';
            this.modal.activeGroup.style.display = 'none';
        }

        this.modal.wrap?.classList.remove('hidden');
    }

    _hideModal() {
        this.modal.wrap?.classList.add('hidden');
    }

    async _submit(e) {
        e.preventDefault();

        const id = this.modal.id.value;
        const fullName = this.modal.fullName.value.trim();

        if (!fullName) {
            showError('Укажите ФИО врача');
            return;
        }

        try {
            if (id) {
                await apiFetch(`/doctor/${id}`, {
                    method: 'PUT',
                    body: JSON.stringify({ fullName, isActive: this.modal.active.checked })
                });
                showSuccess('Данные врача обновлены');
            } else {
                await apiFetch('/doctor', {
                    method: 'POST',
                    body: JSON.stringify({ fullName })
                });
                showSuccess('Врач добавлен');
            }

            this._hideModal();
            this.loadAll();
            // Обновляем выпадающие списки врачей в других разделах панели
            // (запись по телефону, календарь, редактирование заявки).
            window.reloadDoctorSelects?.();
        } catch (err) {
            showError(err.message || 'Не удалось сохранить врача');
        }
    }

    async _toggleActive(doctor) {
        if (!doctor) return;
        const willActivate = !doctor.isActive;

        if (!confirm(willActivate
            ? `Снова показывать врача "${doctor.fullName}" в записи и на сайте?`
            : `Скрыть врача "${doctor.fullName}" из записи и с сайта? История его приёмов сохранится.`)) {
            return;
        }

        try {
            await apiFetch(`/doctor/${doctor.id}`, {
                method: 'PUT',
                body: JSON.stringify({ isActive: willActivate })
            });
            showSuccess(willActivate ? 'Врач снова активен' : 'Врач деактивирован');
            this.loadAll();
            window.reloadDoctorSelects?.();
        } catch (err) {
            showError(err.message || 'Не удалось изменить статус врача');
        }
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const manager = new DoctorsManager();
    manager.init();
    window.DoctorsManagerInstance = manager;
});

export { DoctorsManager };