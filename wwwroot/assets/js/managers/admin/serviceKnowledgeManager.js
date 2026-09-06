import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, escapeHtml } from '../../services/ui.js';
import { buildServicePayload, formatServicePrice } from './serviceKnowledgeUtils.js';

class ServiceKnowledgeManager {
    constructor() {
        this.services = [];
        this.search = '';
        this.status = 'all';
        this.section = null;
        this.tbody = null;
        this.modal = null;
        this.form = null;
    }

    init() {
        if (!document.querySelector('.panel-nav') || document.getElementById('section-services-knowledge')) return;
        this._injectStyles();
        this._injectNavigation();
        this._injectSection();
        this._injectModal();
        this._bindNavigation();
        this._bindControls();

        if (sessionStorage.getItem('admin_active_section') === 'services-knowledge') {
            this._showSection();
        }
    }

    _injectNavigation() {
        const nav = document.querySelector('.panel-nav');
        const doctors = nav.querySelector('[data-section="doctors"]');
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'panel-nav-link';
        button.dataset.section = 'services-knowledge';
        button.textContent = 'Услуги / база Денты';
        if (doctors) nav.insertBefore(button, doctors);
        else nav.appendChild(button);
        this.navButton = button;
    }

    _injectSection() {
        const container = document.querySelector('.panel-content-inner');
        this.section = document.createElement('section');
        this.section.className = 'panel-section hidden';
        this.section.id = 'section-services-knowledge';
        this.section.innerHTML = `
            <div class="panel-section-header">
                <h2>Услуги и база знаний Денты</h2>
                <p>Цены, описания, ключевые слова и ссылки. После сохранения изменения автоматически попадают в базу знаний AI-ассистента.</p>
            </div>
            <div class="panel-card knowledge-info-card">
                <strong>Как это работает:</strong> активные позиции используются на сайте и в ответах Денты. Ключевые слова помогают ассистенту находить нужную услугу, а локальная ссылка ведёт пациента на правильную страницу.
            </div>
            <div class="panel-card">
                <div class="knowledge-toolbar">
                    <button type="button" class="panel-btn-primary" id="knowledge-add-service">+ Добавить услугу</button>
                    <input type="search" id="knowledge-search" placeholder="Поиск по услуге, категории или ключевым словам">
                    <select id="knowledge-status">
                        <option value="all">Все статусы</option>
                        <option value="active">Активные</option>
                        <option value="inactive">Отключённые</option>
                    </select>
                </div>
                <div class="knowledge-summary" id="knowledge-summary">Загрузка…</div>
                <div class="panel-table-wrap">
                    <table class="panel-table">
                        <thead><tr>
                            <th>ID</th><th>Категория / услуга</th><th>Цена</th><th>База Денты</th><th>Статус</th><th class="col-actions">Действия</th>
                        </tr></thead>
                        <tbody id="knowledge-services-body"><tr><td colspan="6">Загрузка…</td></tr></tbody>
                    </table>
                </div>
            </div>`;
        container.appendChild(this.section);
        this.tbody = this.section.querySelector('#knowledge-services-body');
    }

    _injectModal() {
        this.modal = document.createElement('div');
        this.modal.className = 'panel-modal hidden';
        this.modal.id = 'service-knowledge-modal';
        this.modal.innerHTML = `
            <div class="panel-modal-backdrop" data-close-service-modal></div>
            <div class="panel-modal-dialog knowledge-modal-dialog">
                <div class="panel-modal-header">
                    <h3 id="service-knowledge-modal-title">Добавить услугу</h3>
                    <button type="button" class="panel-modal-close" data-close-service-modal>&times;</button>
                </div>
                <div class="panel-modal-body">
                    <form id="service-knowledge-form" class="panel-form">
                        <input type="hidden" name="id">
                        <div class="knowledge-form-grid">
                            <div class="panel-form-group"><label>Категория *</label><input name="category" maxlength="120" required></div>
                            <div class="panel-form-group"><label>Название *</label><input name="name" maxlength="180" required></div>
                            <div class="panel-form-group"><label>Цена от, ₽ *</label><input name="priceFrom" inputmode="decimal" required></div>
                            <div class="panel-form-group"><label>Цена до, ₽</label><input name="priceTo" inputmode="decimal" placeholder="необязательно"></div>
                            <div class="panel-form-group"><label>Единица</label><input name="unit" maxlength="80" placeholder="зуб, процедура, курс"></div>
                            <div class="panel-form-group"><label>Порядок</label><input name="sortOrder" type="number" value="0"></div>
                        </div>
                        <div class="panel-form-group"><label>Ключевые слова для Денты</label><input name="keywords" maxlength="500" placeholder="имплант, all-on-4, отсутствует зуб"></div>
                        <div class="panel-form-group"><label>Локальная страница</label><input name="pageUrl" maxlength="300" placeholder="/pages/services/implants.html"></div>
                        <div class="panel-form-group"><label>Описание</label><textarea name="description" rows="4" maxlength="1000"></textarea></div>
                        <div class="panel-form-group hidden" id="service-knowledge-active-group"><label><input name="isActive" type="checkbox"> Активна на сайте и в базе Денты</label></div>
                        <div class="panel-modal-footer">
                            <button type="button" class="panel-btn-secondary" data-close-service-modal>Отмена</button>
                            <button type="submit" class="panel-btn-primary">Сохранить</button>
                        </div>
                    </form>
                </div>
            </div>`;
        document.body.appendChild(this.modal);
        this.form = this.modal.querySelector('#service-knowledge-form');
    }

    _bindNavigation() {
        this.navButton.addEventListener('click', () => {
            this._showSection();
            this.loadAll();
        });
        document.querySelectorAll('.panel-nav-link').forEach(button => {
            if (button === this.navButton) return;
            button.addEventListener('click', () => this.section?.classList.add('hidden'));
        });
    }

    _showSection() {
        document.querySelectorAll('.panel-section').forEach(section => section.classList.add('hidden'));
        document.querySelectorAll('.panel-nav-link').forEach(button => button.classList.remove('active'));
        this.section.classList.remove('hidden');
        this.navButton.classList.add('active');
        sessionStorage.setItem('admin_active_section', 'services-knowledge');
    }

    _bindControls() {
        this.section.querySelector('#knowledge-add-service').addEventListener('click', () => this._openModal());
        this.section.querySelector('#knowledge-search').addEventListener('input', event => {
            this.search = event.target.value.toLowerCase().trim();
            this._render();
        });
        this.section.querySelector('#knowledge-status').addEventListener('change', event => {
            this.status = event.target.value;
            this._render();
        });
        this.modal.querySelectorAll('[data-close-service-modal]').forEach(button =>
            button.addEventListener('click', () => this._closeModal()));
        this.form.addEventListener('submit', event => this._submit(event));
    }

    async loadAll() {
        this.tbody.innerHTML = '<tr><td colspan="6">Загрузка…</td></tr>';
        try {
            this.services = await apiFetch('/service/admin/all');
            this._render();
        } catch (error) {
            console.error('ServiceKnowledgeManager load error:', error);
            this.tbody.innerHTML = '<tr><td colspan="6">Ошибка загрузки базы услуг</td></tr>';
            showError('Не удалось загрузить услуги');
        }
    }

    _filtered() {
        return this.services.filter(service => {
            if (this.status === 'active' && !service.isActive) return false;
            if (this.status === 'inactive' && service.isActive) return false;
            if (!this.search) return true;
            const haystack = [service.name, service.category, service.keywords, service.description]
                .map(value => String(value || '').toLowerCase()).join(' ');
            return haystack.includes(this.search);
        });
    }

    _render() {
        const rows = this._filtered();
        const active = this.services.filter(service => service.isActive).length;
        const summary = this.section.querySelector('#knowledge-summary');
        summary.textContent = `${rows.length} показано · ${this.services.length} всего · ${active} активных для сайта и Денты`;

        if (!rows.length) {
            this.tbody.innerHTML = '<tr><td colspan="6">Ничего не найдено</td></tr>';
            return;
        }

        this.tbody.innerHTML = rows.map(service => {
            const knowledge = [service.keywords, service.pageUrl].filter(Boolean).map(escapeHtml).join('<br>') || '—';
            return `<tr data-service-id="${service.id}">
                <td>${service.id}</td>
                <td><strong>${escapeHtml(service.category)}</strong><br>${escapeHtml(service.name)}</td>
                <td>${escapeHtml(formatServicePrice(service))}</td>
                <td class="knowledge-cell">${knowledge}</td>
                <td><span class="status-badge ${service.isActive ? 'status-confirmed' : 'status-cancelled'}">${service.isActive ? 'Активна' : 'Отключена'}</span></td>
                <td><div class="panel-table-actions">
                    <button type="button" class="btn-tag btn-edit" data-edit-service="${service.id}" title="Редактировать">✏️</button>
                    <button type="button" class="btn-tag ${service.isActive ? 'btn-cancel' : 'btn-confirm'}" data-toggle-service="${service.id}" title="${service.isActive ? 'Отключить' : 'Активировать'}">${service.isActive ? '✕' : '✓'}</button>
                </div></td>
            </tr>`;
        }).join('');

        this.tbody.querySelectorAll('[data-edit-service]').forEach(button =>
            button.addEventListener('click', () => this._openModal(this._find(button.dataset.editService))));
        this.tbody.querySelectorAll('[data-toggle-service]').forEach(button =>
            button.addEventListener('click', () => this._toggle(this._find(button.dataset.toggleService))));
    }

    _find(id) {
        return this.services.find(service => Number(service.id) === Number(id));
    }

    _openModal(service = null) {
        this.form.reset();
        this.form.elements.id.value = service?.id || '';
        this.form.elements.category.value = service?.category || '';
        this.form.elements.name.value = service?.name || '';
        this.form.elements.priceFrom.value = service?.priceFrom ?? '';
        this.form.elements.priceTo.value = service?.priceTo ?? '';
        this.form.elements.unit.value = service?.unit || '';
        this.form.elements.sortOrder.value = service?.sortOrder ?? 0;
        this.form.elements.keywords.value = service?.keywords || '';
        this.form.elements.pageUrl.value = service?.pageUrl || '';
        this.form.elements.description.value = service?.description || '';
        this.form.elements.isActive.checked = service?.isActive ?? true;
        this.modal.querySelector('#service-knowledge-modal-title').textContent = service ? 'Редактировать услугу' : 'Добавить услугу';
        this.modal.querySelector('#service-knowledge-active-group').classList.toggle('hidden', !service);
        this.modal.classList.remove('hidden');
    }

    _closeModal() {
        this.modal.classList.add('hidden');
    }

    async _submit(event) {
        event.preventDefault();
        const formData = Object.fromEntries(new FormData(this.form).entries());
        formData.isActive = this.form.elements.isActive.checked;
        const id = this.form.elements.id.value;
        const result = buildServicePayload(formData, { edit: Boolean(id) });
        if (!result.ok) {
            showError(result.error);
            return;
        }

        const submit = this.form.querySelector('[type="submit"]');
        submit.disabled = true;
        try {
            await apiFetch(id ? `/service/${id}` : '/service', {
                method: id ? 'PUT' : 'POST',
                body: JSON.stringify(result.payload),
            });
            showSuccess(id ? 'Услуга и база Денты обновлены' : 'Услуга добавлена в базу Денты');
            this._closeModal();
            await this.loadAll();
        } catch (error) {
            showError(error.message || 'Не удалось сохранить услугу');
        } finally {
            submit.disabled = false;
        }
    }

    async _toggle(service) {
        if (!service) return;
        const next = !service.isActive;
        if (!confirm(next
            ? `Активировать «${service.name}» на сайте и в базе Денты?`
            : `Отключить «${service.name}» на сайте и в базе Денты?`)) return;

        try {
            await apiFetch(`/service/${service.id}`, {
                method: 'PUT',
                body: JSON.stringify({ isActive: next }),
            });
            showSuccess(next ? 'Услуга активирована' : 'Услуга отключена');
            await this.loadAll();
        } catch (error) {
            showError(error.message || 'Не удалось изменить статус услуги');
        }
    }

    _injectStyles() {
        if (document.getElementById('service-knowledge-styles')) return;
        const style = document.createElement('style');
        style.id = 'service-knowledge-styles';
        style.textContent = `
            .knowledge-toolbar{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:12px}
            .knowledge-toolbar input[type=search]{min-width:240px;flex:1}
            .knowledge-toolbar input,.knowledge-toolbar select{padding:10px 12px;border:1px solid #d9e5e1;border-radius:8px;background:#fff}
            .knowledge-summary{font-size:.86rem;color:#687a75;margin:8px 0 14px}
            .knowledge-info-card{font-size:.92rem;line-height:1.5;border-left:4px solid #13b39b}
            .knowledge-cell{max-width:260px;white-space:normal;word-break:break-word}
            .knowledge-form-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}
            .knowledge-modal-dialog{max-width:720px}
            @media(max-width:700px){.knowledge-form-grid{grid-template-columns:1fr}.knowledge-toolbar>*{width:100%}}
        `;
        document.head.appendChild(style);
    }
}

export function installServiceKnowledgeManager() {
    if (typeof document === 'undefined' || typeof window === 'undefined') return;
    document.addEventListener('DOMContentLoaded', () => {
        const manager = new ServiceKnowledgeManager();
        manager.init();
        window.ServiceKnowledgeManagerInstance = manager;
    });
}

export { ServiceKnowledgeManager, buildServicePayload, formatServicePrice };
