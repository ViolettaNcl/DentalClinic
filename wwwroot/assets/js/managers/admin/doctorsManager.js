import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, escapeHtml } from '../../services/ui.js';
import { installDoctorCalendarAvailability } from './doctorCalendarAvailability.js';
import { installAdminLogoutGuard } from './adminLogoutGuard.js';
import { installAdminAnalyticsSummary } from './adminAnalyticsSummary.js';
import { installAdminAnalyticsCanvasGuard } from './adminAnalyticsCanvasGuard.js';
import { installServiceKnowledgeManager } from './serviceKnowledgeManager.js';
import { installClinicKnowledgeManager } from './clinicKnowledgeManager.js';
import { buildDoctorPayload, formatDoctorKnowledgeSummary } from './doctorKnowledgeUtils.js';
import { runWhenDomReady } from '../../core/domReady.js';

installAdminLogoutGuard();
installAdminAnalyticsSummary();
installAdminAnalyticsCanvasGuard();
installServiceKnowledgeManager();
installClinicKnowledgeManager();

class DoctorsManager {
    constructor() {
        this.tbody = document.getElementById('admin-doctors-body');
        this.addBtn = document.getElementById('btn-add-doctor');
        this._loadedOnce = false;
        this._loadPromise = null;
        this._cache = {};

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

        this._ensureProfileFields();
        this._injectStyles();
        this.addBtn?.addEventListener('click', () => this._openModal());
        this.modal.close?.addEventListener('click', () => this._hideModal());
        this.modal.cancel?.addEventListener('click', () => this._hideModal());
        this.modal.form?.addEventListener('submit', e => this._submit(e));
        this.modal.photoFile?.addEventListener('change', () => this._previewSelectedPhoto());

        const nav = document.querySelector('.panel-nav-link[data-section="doctors"]');
        nav?.addEventListener('click', () => this.loadOnce());
        if (sessionStorage.getItem('admin_active_section') === 'doctors') this.loadOnce();
    }

    loadOnce() {
        if (this._loadedOnce) return Promise.resolve(true);
        if (this._loadPromise) return this._loadPromise;

        this._loadPromise = this.loadAll()
            .then(success => {
                if (success) this._loadedOnce = true;
                return success;
            })
            .finally(() => { this._loadPromise = null; });
        return this._loadPromise;
    }

    _ensureProfileFields() {
        if (!this.modal.form) return;
        if (!document.getElementById('doctor-specialization')) {
            this.modal.activeGroup?.insertAdjacentHTML('beforebegin', `
                <div class="doctor-profile-editor-intro">
                    <div class="doctor-profile-editor-intro__icon">🩺</div>
                    <div>
                        <strong>Публичная карточка врача</strong>
                        <small>Заполните профиль один раз — эти данные используются на странице «Врачи», в записи и в базе знаний Денты.</small>
                    </div>
                </div>

                <div class="doctor-photo-editor">
                    <div class="doctor-photo-preview" id="doctor-photo-preview"><span>Фото</span></div>
                    <div class="doctor-photo-controls">
                        <label class="panel-btn-secondary doctor-photo-upload">
                            <span>📷 Выбрать фотографию</span>
                            <input type="file" id="doctor-photo-file" accept="image/jpeg,image/png,image/webp" hidden>
                        </label>
                        <small>JPG, PNG или WEBP · до 5 МБ. Лучше вертикальное фото 4:5.</small>
                        <label class="doctor-remove-photo-row" id="doctor-remove-photo-row" hidden>
                            <input type="checkbox" id="doctor-remove-photo"> Удалить текущее фото
                        </label>
                    </div>
                </div>

                <details class="doctor-localized-names">
                    <summary>Имя врача на других языках</summary>
                    <small>Необязательно. Если поле пустое, используется основное имя.</small>
                    <div class="doctor-localized-grid">
                        <label>English<input type="text" id="doctor-fullname-en" maxlength="150" autocomplete="off"></label>
                        <label>Français<input type="text" id="doctor-fullname-fr" maxlength="150" autocomplete="off"></label>
                        <label>Ελληνικά<input type="text" id="doctor-fullname-el" maxlength="150" autocomplete="off"></label>
                        <label>العربية<input type="text" id="doctor-fullname-ar" maxlength="150" autocomplete="off" dir="rtl"></label>
                    </div>
                </details>

                <div class="doctor-editor-grid">
                    <div class="panel-form-group doctor-editor-span-2">
                        <label for="doctor-role-title">Профессиональный заголовок</label>
                        <input type="text" id="doctor-role-title" maxlength="300" placeholder="Хирург-имплантолог · Эстетолог · Врач высшей категории">
                    </div>
                    <div class="panel-form-group doctor-editor-span-2">
                        <label for="doctor-specialization">Специализация для поиска и Денты</label>
                        <input type="text" id="doctor-specialization" maxlength="300" placeholder="имплантология, хирургия, эстетика">
                    </div>
                    <div class="panel-form-group">
                        <label for="doctor-experience">Стаж, лет</label>
                        <input type="number" id="doctor-experience" min="0" max="80" step="1" placeholder="5">
                    </div>
                    <div class="panel-form-group doctor-editor-span-2">
                        <label for="doctor-bio">Описание</label>
                        <textarea id="doctor-bio" rows="3" maxlength="500" placeholder="Короткое профессиональное описание для карточки врача"></textarea>
                    </div>
                    <div class="panel-form-group">
                        <label for="doctor-education">Образование</label>
                        <textarea id="doctor-education" rows="5" maxlength="1200" placeholder="Каждый пункт с новой строки&#10;Волгоградский ГМУ — факультет стоматологии&#10;Ординатура — хирургическая стоматология"></textarea>
                    </div>
                    <div class="panel-form-group">
                        <label for="doctor-skills">Специализации на карточке</label>
                        <textarea id="doctor-skills" rows="5" maxlength="1200" placeholder="Каждый пункт с новой строки&#10;Дентальная имплантация&#10;Костная пластика"></textarea>
                    </div>
                    <div class="panel-form-group doctor-editor-span-2">
                        <label for="doctor-philosophy">Философия / цитата</label>
                        <textarea id="doctor-philosophy" rows="2" maxlength="500" placeholder="Короткая фраза о подходе врача"></textarea>
                    </div>
                    <div class="doctor-card-metrics doctor-editor-span-2">
                        <div class="panel-form-group">
                            <label for="doctor-stat2-value">Показатель №2</label>
                            <input type="text" id="doctor-stat2-value" maxlength="40" placeholder="100+">
                        </div>
                        <div class="panel-form-group">
                            <label for="doctor-stat2-label">Подпись №2</label>
                            <input type="text" id="doctor-stat2-label" maxlength="80" placeholder="Имплантов">
                        </div>
                        <div class="panel-form-group">
                            <label for="doctor-stat3-value">Показатель №3</label>
                            <input type="text" id="doctor-stat3-value" maxlength="40" placeholder="5/5">
                        </div>
                        <div class="panel-form-group">
                            <label for="doctor-stat3-label">Подпись №3</label>
                            <input type="text" id="doctor-stat3-label" maxlength="80" placeholder="Рейтинг">
                        </div>
                    </div>
                </div>
            `);
        }

        const ids = {
            fullNameEn: 'doctor-fullname-en', fullNameFr: 'doctor-fullname-fr',
            fullNameEl: 'doctor-fullname-el', fullNameAr: 'doctor-fullname-ar',
            roleTitle: 'doctor-role-title', specialization: 'doctor-specialization',
            experienceYears: 'doctor-experience', bio: 'doctor-bio',
            education: 'doctor-education', skills: 'doctor-skills', philosophy: 'doctor-philosophy',
            stat2Value: 'doctor-stat2-value', stat2Label: 'doctor-stat2-label',
            stat3Value: 'doctor-stat3-value', stat3Label: 'doctor-stat3-label',
            photoFile: 'doctor-photo-file', photoPreview: 'doctor-photo-preview',
            removePhoto: 'doctor-remove-photo', removePhotoRow: 'doctor-remove-photo-row',
        };
        Object.entries(ids).forEach(([key, id]) => { this.modal[key] = document.getElementById(id); });
        this.modal.fullName?.setAttribute('maxlength', '150');
    }

    async loadAll() {
        this.tbody.innerHTML = `<tr><td colspan="4">Загрузка...</td></tr>`;
        try {
            const doctors = await apiFetch('/doctor/admin/all');
            this._render(doctors || []);
            return true;
        } catch (err) {
            console.error('DoctorsManager loadAll error:', err);
            this.tbody.innerHTML = `<tr><td colspan="4" class="panel-error"><div class="panel-inline-retry">
                <span>${escapeHtml(err?.message || 'Не удалось загрузить список врачей')}</span>
                <button type="button" class="panel-btn-secondary" data-retry-doctors>Повторить</button>
            </div></td></tr>`;
            this.tbody.querySelector('[data-retry-doctors]')?.addEventListener('click', () => this.loadAll());
            showError(err?.message || 'Не удалось загрузить список врачей');
            return false;
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
                    <div class="doctor-admin-name-cell">
                        <div class="doctor-admin-thumb">${d.photoUrl
                            ? `<img src="${escapeHtml(d.photoUrl)}" alt="">`
                            : `<span>${escapeHtml(String(d.fullName || '?').trim().charAt(0).toUpperCase())}</span>`}</div>
                        <div><strong>${escapeHtml(d.fullName)}</strong>
                        <div class="doctor-knowledge-summary">${escapeHtml(formatDoctorKnowledgeSummary(d))}</div></div>
                    </div>
                </td>
                <td><span class="status-badge ${d.isActive ? 'status-confirmed' : 'status-cancelled'}">${d.isActive ? 'Активен' : 'Отключён'}</span></td>
                <td class="col-actions"><div class="panel-table-actions">
                    <button class="btn-tag btn-edit" data-action="edit" title="Редактировать полную карточку врача">✏️</button>
                    <button class="btn-tag ${d.isActive ? 'btn-cancel' : 'btn-confirm'}" data-action="toggle" title="${d.isActive ? 'Деактивировать' : 'Активировать'}">${d.isActive ? '✕' : '✓'}</button>
                </div></td>
            </tr>`).join('');

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
        const values = {
            id: doctor?.id || '', fullName: doctor?.fullName || '',
            fullNameEn: doctor?.fullNameEn || '', fullNameFr: doctor?.fullNameFr || '',
            fullNameEl: doctor?.fullNameEl || '', fullNameAr: doctor?.fullNameAr || '',
            roleTitle: doctor?.roleTitle || '', specialization: doctor?.specialization || '',
            experienceYears: doctor?.experienceYears ?? '', bio: doctor?.bio || '',
            education: doctor?.education || '', skills: doctor?.skills || '', philosophy: doctor?.philosophy || '',
            stat2Value: doctor?.stat2Value || '', stat2Label: doctor?.stat2Label || '',
            stat3Value: doctor?.stat3Value || '', stat3Label: doctor?.stat3Label || '',
        };
        this.modal.id.value = values.id;
        this.modal.fullName.value = values.fullName;
        Object.entries(values).forEach(([key, value]) => {
            if (key !== 'id' && key !== 'fullName' && this.modal[key]) this.modal[key].value = value;
        });

        if (this.modal.removePhoto) this.modal.removePhoto.checked = false;
        this._setPhotoPreview(doctor?.photoUrl || null, doctor?.fullName || 'Фото');
        if (this.modal.removePhotoRow) this.modal.removePhotoRow.hidden = !doctor?.photoUrl;

        if (doctor) {
            this.modal.title.textContent = 'Редактировать карточку врача';
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
        if (this.modal.photoFile) this.modal.photoFile.value = '';
    }

    _setPhotoPreview(url, name = 'Фото') {
        if (!this.modal.photoPreview) return;
        if (!url) {
            this.modal.photoPreview.innerHTML = `<span>${escapeHtml(String(name).trim().charAt(0).toUpperCase() || 'Фото')}</span>`;
            return;
        }
        this.modal.photoPreview.innerHTML = `<img src="${escapeHtml(url)}" alt="${escapeHtml(name)}">`;
    }

    _previewSelectedPhoto() {
        const file = this.modal.photoFile?.files?.[0];
        if (!file) return;
        if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
            showError('Разрешены только JPG, PNG и WEBP');
            this.modal.photoFile.value = '';
            return;
        }
        if (file.size > 5 * 1024 * 1024) {
            showError('Фотография должна быть не больше 5 МБ');
            this.modal.photoFile.value = '';
            return;
        }
        const url = URL.createObjectURL(file);
        this._setPhotoPreview(url, this.modal.fullName?.value || 'Фото врача');
        setTimeout(() => URL.revokeObjectURL(url), 10000);
    }

    async _submit(e) {
        e.preventDefault();
        const id = this.modal.id.value;
        const values = {
            fullName: this.modal.fullName.value,
            fullNameEn: this.modal.fullNameEn.value,
            fullNameFr: this.modal.fullNameFr.value,
            fullNameEl: this.modal.fullNameEl.value,
            fullNameAr: this.modal.fullNameAr.value,
            roleTitle: this.modal.roleTitle.value,
            specialization: this.modal.specialization.value,
            experienceYears: this.modal.experienceYears.value,
            bio: this.modal.bio.value,
            education: this.modal.education.value,
            skills: this.modal.skills.value,
            philosophy: this.modal.philosophy.value,
            stat2Value: this.modal.stat2Value.value,
            stat2Label: this.modal.stat2Label.value,
            stat3Value: this.modal.stat3Value.value,
            stat3Label: this.modal.stat3Label.value,
            isActive: this.modal.active.checked,
        };
        const result = buildDoctorPayload(values, { edit: Boolean(id) });
        if (!result.ok) { showError(result.error); return; }

        const submit = this.modal.form.querySelector('[type="submit"]');
        submit.disabled = true;
        try {
            const saved = id
                ? await apiFetch(`/doctor/${id}`, { method: 'PUT', body: JSON.stringify(result.payload) })
                : await apiFetch('/doctor', { method: 'POST', body: JSON.stringify(result.payload) });
            const doctorId = Number(id || saved?.id);

            if (doctorId && this.modal.removePhoto?.checked && !this.modal.photoFile?.files?.[0]) {
                await apiFetch(`/doctor/${doctorId}/photo`, { method: 'DELETE' });
            }

            const photo = this.modal.photoFile?.files?.[0];
            if (doctorId && photo) {
                const formData = new FormData();
                formData.append('file', photo, photo.name);
                await apiFetch(`/doctor/${doctorId}/photo`, { method: 'POST', body: formData, timeoutMs: 60000 });
            }

            showSuccess(id ? 'Карточка врача обновлена' : 'Врач добавлен и карточка создана');
            this._hideModal();
            await this.loadAll();
            window.reloadDoctorSelects?.();
        } catch (err) {
            if (err?.status === 401) showError('Сессия администратора истекла. Войдите снова и повторите сохранение.');
            else showError(err?.message || 'Не удалось сохранить врача');
        } finally {
            submit.disabled = false;
        }
    }

    async _toggleActive(doctor) {
        if (!doctor) return;
        const willActivate = !doctor.isActive;
        if (!confirm(willActivate
            ? `Снова показывать врача "${doctor.fullName}" в записи, на сайте и в базе Денты?`
            : `Скрыть врача "${doctor.fullName}" из записи, сайта и базы Денты? История приёмов сохранится.`)) return;

        try {
            await apiFetch(`/doctor/${doctor.id}`, { method: 'PUT', body: JSON.stringify({ isActive: willActivate }) });
            showSuccess(willActivate ? 'Врач снова активен' : 'Врач деактивирован');
            await this.loadAll();
            window.reloadDoctorSelects?.();
        } catch (err) {
            showError(err?.message || 'Не удалось изменить статус врача');
        }
    }

    _injectStyles() {
        if (document.getElementById('doctor-profile-editor-styles')) return;
        const style = document.createElement('style');
        style.id = 'doctor-profile-editor-styles';
        style.textContent = `
            #doctor-modal .panel-modal-dialog{width:min(94vw,900px);max-width:900px}
            #doctor-modal .panel-modal-body{max-height:min(78vh,820px);overflow:auto;overscroll-behavior:contain}
            .doctor-profile-editor-intro{display:flex;gap:12px;align-items:center;padding:12px 14px;margin-bottom:14px;border-radius:14px;background:linear-gradient(135deg,#effbf8,#f7fffd);border:1px solid #cdece5;color:#315d54}
            .doctor-profile-editor-intro__icon{display:grid;place-items:center;width:42px;height:42px;border-radius:12px;background:#12aa94;color:white;font-size:1.25rem;flex:0 0 auto}
            .doctor-profile-editor-intro small{display:block;margin-top:3px;color:#687a75;line-height:1.45}
            .doctor-photo-editor{display:grid;grid-template-columns:150px 1fr;gap:18px;align-items:center;padding:14px;border:1px solid #dce9e5;border-radius:16px;background:#fbfefd;margin-bottom:14px}
            .doctor-photo-preview{width:136px;aspect-ratio:4/5;border-radius:16px;overflow:hidden;display:grid;place-items:center;background:linear-gradient(145deg,#e9f7f4,#d5ece6);color:#147f70;font:800 2.5rem/1 Manrope,sans-serif;border:1px solid #cce8e1}
            .doctor-photo-preview img{width:100%;height:100%;object-fit:cover}
            .doctor-photo-controls{display:grid;gap:8px;align-content:center}.doctor-photo-upload{display:inline-flex!important;width:max-content;cursor:pointer}.doctor-photo-controls small{color:#71827e}
            .doctor-remove-photo-row{display:flex;gap:7px;align-items:center;color:#a14f4f;font-size:.88rem}
            .doctor-localized-names{margin:4px 0 14px;padding:12px 14px;border:1px solid #dce9e5;border-radius:12px;background:#fbfefd}.doctor-localized-names summary{cursor:pointer;font-weight:700;color:#315d54}.doctor-localized-names>small{display:block;margin:8px 0;color:#687a75}.doctor-localized-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.doctor-localized-grid label{display:grid;gap:5px;font-size:.84rem;color:#51645f}.doctor-localized-grid input{width:100%}
            .doctor-editor-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.doctor-editor-span-2{grid-column:1/-1}.doctor-card-metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;padding:12px;border:1px dashed #bddfd7;border-radius:14px;background:#f8fcfb}
            .doctor-admin-name-cell{display:flex;align-items:center;gap:10px}.doctor-admin-thumb{width:42px;height:42px;border-radius:12px;overflow:hidden;display:grid;place-items:center;flex:0 0 auto;background:#e5f6f2;color:#0c8d7a;font-weight:800}.doctor-admin-thumb img{width:100%;height:100%;object-fit:cover}.doctor-knowledge-summary{margin-top:4px;color:#687a75;font-size:.82rem;line-height:1.35}.panel-inline-retry{display:flex;align-items:center;justify-content:center;gap:12px;flex-wrap:wrap;padding:10px}
            @media(max-width:720px){#doctor-modal .panel-modal-dialog{width:96vw}.doctor-photo-editor{grid-template-columns:110px 1fr}.doctor-photo-preview{width:100px}.doctor-editor-grid,.doctor-localized-grid{grid-template-columns:1fr}.doctor-editor-span-2{grid-column:auto}.doctor-card-metrics{grid-template-columns:repeat(2,minmax(0,1fr))}}
            @media(max-width:480px){.doctor-photo-editor{grid-template-columns:1fr}.doctor-photo-preview{width:120px;margin:auto}.doctor-photo-controls{justify-items:center;text-align:center}.doctor-card-metrics{grid-template-columns:1fr}}
        `;
        document.head.append(style);
    }
}

runWhenDomReady(() => {
    const manager = new DoctorsManager();
    manager.init();
    window.DoctorsManagerInstance = manager;
    installDoctorCalendarAvailability();
});

export { DoctorsManager };
