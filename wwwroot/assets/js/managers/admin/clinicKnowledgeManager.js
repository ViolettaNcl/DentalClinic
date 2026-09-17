import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, escapeHtml } from '../../services/ui.js';
import { buildClinicKnowledgePayload, CLINIC_KNOWLEDGE_LIMITS } from './clinicKnowledgeUtils.js';
import { runWhenDomReady } from '../../core/domReady.js';

class ClinicKnowledgeManager {
    constructor() {
        this.items = [];
        this.search = '';
        this.status = 'all';
        this.section = null;
        this.modal = null;
        this.form = null;
        this.navButton = null;
    }

    init() {
        if (!document.querySelector('.panel-nav') || document.getElementById('section-clinic-knowledge')) return;
        this._injectStyles();
        this._injectNavigation();
        this._injectSection();
        this._injectModal();
        this._bindNavigation();
        this._bindControls();

        if (sessionStorage.getItem('admin_active_section') === 'clinic-knowledge') {
            this._showSection();
            this.loadAll();
        }
    }

    _injectNavigation() {
        const nav = document.querySelector('.panel-nav');
        const doctors = nav.querySelector('[data-section="doctors"]');
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'panel-nav-link';
        button.dataset.section = 'clinic-knowledge';
        button.textContent = 'FAQ / база Денты';
        if (doctors) nav.insertBefore(button, doctors);
        else nav.appendChild(button);
        this.navButton = button;
    }

    _injectSection() {
        const container = document.querySelector('.panel-content-inner');
        this.section = document.createElement('section');
        this.section.className = 'panel-section hidden';
        this.section.id = 'section-clinic-knowledge';
        this.section.innerHTML = `
            <div class="panel-section-header">
                <h2>FAQ и база знаний Денты</h2>
                <p>Подтверждённые факты клиники: подготовка к визиту, правила записи, оплаты, FAQ и другая операционная информация.</p>
            </div>
            <div class="panel-card clinic-knowledge-info">
                <strong>Важно:</strong> добавляйте только проверенные сведения клиники. Эти записи автоматически доступны Denta, но не могут переопределять правила клинической безопасности ассистента.
            </div>
            <div class="panel-card">
                <div class="clinic-knowledge-toolbar">
                    <button type="button" class="panel-btn-primary" id="clinic-knowledge-add">+ Добавить факт</button>
                    <input type="search" id="clinic-knowledge-search" placeholder="Поиск по категории, заголовку, тексту или ключевым словам">
                    <select id="clinic-knowledge-status">
                        <option value="all">Все статусы</option>
                        <option value="active">Активные для Денты</option>
                        <option value="inactive">Отключённые</option>
                    </select>
                </div>
                <div class="clinic-knowledge-summary" id="clinic-knowledge-summary">Загрузка…</div>
                <div class="panel-table-wrap">
                    <table class="panel-table clinic-knowledge-table">
                        <thead><tr>
                            <th>ID</th><th>Категория / заголовок</th><th>Содержание</th><th>Ключевые слова</th><th>Статус</th><th class="col-actions">Действия</th>
                        </tr></thead>
                        <tbody id="clinic-knowledge-body"><tr><td colspan="6">Загрузка…</td></tr></tbody>
                    </table>
                </div>
            </div>`;
        container.appendChild(this.section);
        this.tbody = this.section.querySelector('#clinic-knowledge-body');
    }

    _injectModal() {
        this.modal = document.createElement('div');
        this.modal.className = 'panel-modal hidden';
        this.modal.id = 'clinic-knowledge-modal';
        this.modal.innerHTML = `
            <div class="panel-modal-backdrop" data-close-clinic-knowledge></div>
            <div class="panel-modal-dialog clinic-knowledge-dialog">
                <div class="panel-modal-header">
                    <h3 id="clinic-knowledge-modal-title">Добавить факт</h3>
                    <button type="button" class="panel-modal-close" data-close-clinic-knowledge>&times;</button>
                </div>
                <div class="panel-modal-body">
                    <form id="clinic-knowledge-form" class="panel-form">
                        <input type="hidden" name="id">
                        <div class="clinic-knowledge-grid">
                            <div class="panel-form-group">
                                <label>Категория *</label>
                                <input name="category" maxlength="${CLINIC_KNOWLEDGE_LIMITS.category}" required placeholder="Например: Оплата, Подготовка, Запись">
                            </div>
                            <div class="panel-form-group">
                                <label>Порядок</label>
                                <input name="sortOrder" type="number" min="0" max="2147483647" step="1" value="0">
                            </div>
                        </div>
                        <div class="panel-form-group">
                            <label>Заголовок / вопрос *</label>
                            <input name="title" maxlength="${CLINIC_KNOWLEDGE_LIMITS.title}" required placeholder="Например: Какие способы оплаты доступны?">
                        </div>
                        <div class="panel-form-group">
                            <label>Подтверждённый ответ *</label>
                            <textarea name="content" rows="7" maxlength="${CLINIC_KNOWLEDGE_LIMITS.content}" required></textarea>
                        </div>
                        <div class="panel-form-group">
                            <label>Ключевые слова</label>
                            <input name="keywords" maxlength="${CLINIC_KNOWLEDGE_LIMITS.keywords}" placeholder="оплата, карта, наличные, payment">
                        </div>
                        <div class="panel-form-group hidden" id="clinic-knowledge-active-group">
                            <label><input name="isActive" type="checkbox"> Активно в базе знаний Denta</label>
                        </div>
                        <div class="panel-modal-footer">
                            <button type="button" class="panel-btn-secondary" data-close-clinic-knowledge>Отмена</button>
                            <button type="submit" class="panel-btn-primary">Сохранить</button>
                        </div>
                    </form>
                </div>
            </div>`;
        document.body.appendChild(this.modal);
        this.form = this.modal.querySelector('#clinic-knowledge-form');
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
        sessionStorage.setItem('admin_active_section', 'clinic-knowledge');
    }

    _bindControls() {
        this.section.querySelector('#clinic-knowledge-add').addEventListener('click', () => this._openModal());
        this.section.querySelector('#clinic-knowledge-search').addEventListener('input', event => {
            this.search = event.target.value.toLowerCase().trim();
            this._render();
        });
        this.section.querySelector('#clinic-knowledge-status').addEventListener('change', event => {
            this.status = event.target.value;
            this._render();
        });
        this.modal.querySelectorAll('[data-close-clinic-knowledge]').forEach(button =>
            button.addEventListener('click', () => this._closeModal()));
        this.form.addEventListener('submit', event => this._submit(event));
    }

    async loadAll() {
        this.tbody.innerHTML = '<tr><td colspan="6">Загрузка…</td></tr>';
        try {
            this.items = await apiFetch('/clinic-knowledge');
            this._render();
        } catch (error) {
            console.error('ClinicKnowledgeManager load error:', error);
            const message = error?.message || 'Не удалось загрузить базу знаний Denta';
            this.tbody.innerHTML = `<tr><td colspan="6" class="panel-error">${escapeHtml(message)} <button type="button" class="panel-btn-secondary" data-retry-clinic-knowledge>Повторить</button></td></tr>`;
            this.tbody.querySelector('[data-retry-clinic-knowledge]')?.addEventListener('click', () => this.loadAll());
            showError(message);
        }
    }

    _filtered() {
        return this.items.filter(item => {
            if (this.status === 'active' && !item.isActive) return false;
            if (this.status === 'inactive' && item.isActive) return false;
            if (!this.search) return true;
            const haystack = [item.category, item.title, item.content, item.keywords]
                .map(value => String(value || '').toLowerCase()).join(' ');
            return haystack.includes(this.search);
        });
    }

    _render() {
        const rows = this._filtered();
        const active = this.items.filter(item => item.isActive).length;
        this.section.querySelector('#clinic-knowledge-summary').textContent =
            `${rows.length} показано · ${this.items.length} всего · ${active} активных для Denta`;

        if (!rows.length) {
            this.tbody.innerHTML = '<tr><td colspan="6">Ничего не найдено</td></tr>';
            return;
        }

        this.tbody.innerHTML = rows.map(item => `
            <tr data-knowledge-id="${item.id}">
                <td data-label="ID">${item.id}</td>
                <td data-label="Категория / заголовок"><strong>${escapeHtml(item.category)}</strong><br>${escapeHtml(item.title)}<div class="clinic-knowledge-order">Порядок: ${item.sortOrder}</div></td>
                <td data-label="Содержание" class="clinic-knowledge-content">${escapeHtml(item.content)}</td>
                <td data-label="Ключевые слова" class="clinic-knowledge-keywords">${escapeHtml(item.keywords || '—')}</td>
                <td data-label="Статус"><span class="status-badge ${item.isActive ? 'status-confirmed' : 'status-cancelled'}">${item.isActive ? 'Активно' : 'Отключено'}</span></td>
                <td data-label="Действия"><div class="panel-table-actions">
                    <button type="button" class="btn-tag btn-edit" data-edit-knowledge="${item.id}" title="Редактировать">✏️</button>
                    <button type="button" class="btn-tag ${item.isActive ? 'btn-cancel' : 'btn-confirm'}" data-toggle-knowledge="${item.id}" title="${item.isActive ? 'Отключить' : 'Активировать'}">${item.isActive ? '✕' : '✓'}</button>
                </div></td>
            </tr>`).join('');

        this.tbody.querySelectorAll('[data-edit-knowledge]').forEach(button =>
            button.addEventListener('click', () => this._openModal(this._find(button.dataset.editKnowledge))));
        this.tbody.querySelectorAll('[data-toggle-knowledge]').forEach(button =>
            button.addEventListener('click', () => this._toggle(this._find(button.dataset.toggleKnowledge))));
    }

    _find(id) {
        return this.items.find(item => Number(item.id) === Number(id));
    }

    _openModal(item = null) {
        this.form.reset();
        this.form.elements.id.value = item?.id || '';
        this.form.elements.category.value = item?.category || '';
        this.form.elements.title.value = item?.title || '';
        this.form.elements.content.value = item?.content || '';
        this.form.elements.keywords.value = item?.keywords || '';
        this.form.elements.sortOrder.value = item?.sortOrder ?? 0;
        this.form.elements.isActive.checked = item?.isActive ?? true;
        this.modal.querySelector('#clinic-knowledge-modal-title').textContent = item ? 'Редактировать факт' : 'Добавить факт';
        this.modal.querySelector('#clinic-knowledge-active-group').classList.toggle('hidden', !item);
        this.modal.classList.remove('hidden');
    }

    _closeModal() {
        this.modal.classList.add('hidden');
    }

    async _submit(event) {
        event.preventDefault();
        const id = this.form.elements.id.value;
        const result = buildClinicKnowledgePayload({
            category: this.form.elements.category.value,
            title: this.form.elements.title.value,
            content: this.form.elements.content.value,
            keywords: this.form.elements.keywords.value,
            sortOrder: this.form.elements.sortOrder.value,
            isActive: this.form.elements.isActive.checked,
        }, { edit: Boolean(id) });

        if (!result.ok) {
            showError(result.error);
            return;
        }

        const submit = this.form.querySelector('[type="submit"]');
        submit.disabled = true;
        try {
            await apiFetch(id ? `/clinic-knowledge/${id}` : '/clinic-knowledge', {
                method: id ? 'PUT' : 'POST',
                body: JSON.stringify(result.payload),
            });
            showSuccess(id ? 'Факт базы Denta обновлён' : 'Факт добавлен в базу Denta');
            this._closeModal();
            await this.loadAll();
        } catch (error) {
            showError(error.message || 'Не удалось сохранить факт базы знаний');
        } finally {
            submit.disabled = false;
        }
    }

    async _toggle(item) {
        if (!item) return;
        const next = !item.isActive;
        if (!confirm(next
            ? `Активировать «${item.title}» для Denta?`
            : `Отключить «${item.title}» в базе Denta? Запись останется в админ-панели.`)) return;

        const result = buildClinicKnowledgePayload({ ...item, isActive: next }, { edit: true });
        if (!result.ok) {
            showError(result.error);
            return;
        }

        try {
            await apiFetch(`/clinic-knowledge/${item.id}`, {
                method: 'PUT',
                body: JSON.stringify(result.payload),
            });
            showSuccess(next ? 'Факт активирован для Denta' : 'Факт отключён');
            await this.loadAll();
        } catch (error) {
            showError(error.message || 'Не удалось изменить статус факта');
        }
    }

    _injectStyles() {
        if (document.getElementById('clinic-knowledge-styles')) return;
        const style = document.createElement('style');
        style.id = 'clinic-knowledge-styles';
        style.textContent = `
            .clinic-knowledge-toolbar{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:12px}
            .clinic-knowledge-toolbar input[type=search]{min-width:280px;flex:1}
            .clinic-knowledge-toolbar input,.clinic-knowledge-toolbar select{padding:10px 12px;border:1px solid #d9e5e1;border-radius:8px;background:#fff}
            .clinic-knowledge-summary{font-size:.86rem;color:#687a75;margin:8px 0 14px}
            .clinic-knowledge-info{font-size:.92rem;line-height:1.5;border-left:4px solid #13b39b}
            .clinic-knowledge-grid{display:grid;grid-template-columns:2fr 1fr;gap:12px}
            .clinic-knowledge-dialog{max-width:760px}
            .clinic-knowledge-table{table-layout:fixed;width:100%}
            .clinic-knowledge-table th,.clinic-knowledge-table td{white-space:normal;overflow-wrap:anywhere;word-break:normal;vertical-align:top}
            .clinic-knowledge-table th:nth-child(1),.clinic-knowledge-table td:nth-child(1){width:5%}
            .clinic-knowledge-table th:nth-child(2),.clinic-knowledge-table td:nth-child(2){width:19%}
            .clinic-knowledge-table th:nth-child(3),.clinic-knowledge-table td:nth-child(3){width:34%}
            .clinic-knowledge-table th:nth-child(4),.clinic-knowledge-table td:nth-child(4){width:20%}
            .clinic-knowledge-table th:nth-child(5),.clinic-knowledge-table td:nth-child(5){width:12%}
            .clinic-knowledge-table th:nth-child(6),.clinic-knowledge-table td:nth-child(6){width:10%}
            .clinic-knowledge-content{white-space:normal;overflow-wrap:anywhere;line-height:1.45}
            .clinic-knowledge-keywords{white-space:normal;overflow-wrap:anywhere;line-height:1.35}
            .clinic-knowledge-order{margin-top:4px;color:#7b8c87;font-size:.8rem}
            @media(max-width:900px){
                .clinic-knowledge-grid{grid-template-columns:1fr}
                .clinic-knowledge-toolbar>*{width:100%;min-width:0!important}
                .clinic-knowledge-table,.clinic-knowledge-table tbody,.clinic-knowledge-table tr,.clinic-knowledge-table td{display:block;width:100%!important}
                .clinic-knowledge-table thead{display:none}
                .clinic-knowledge-table tr{padding:12px 14px;margin:0 0 12px;border:1px solid #e1ece8;border-radius:12px;background:#fff}
                .clinic-knowledge-table td{display:grid;grid-template-columns:minmax(110px,34%) 1fr;gap:12px;padding:8px 0;border-bottom:1px solid #eef3f1}
                .clinic-knowledge-table td:last-child{border-bottom:0}
                .clinic-knowledge-table td::before{content:attr(data-label);font-size:.74rem;font-weight:800;text-transform:uppercase;color:#4b7a70}
                .clinic-knowledge-table .panel-table-actions{justify-self:start}
            }
        `;
        document.head.appendChild(style);
    }
}

export function installClinicKnowledgeManager() {
    if (typeof document === 'undefined' || typeof window === 'undefined') return;
    runWhenDomReady(() => {
        const manager = new ClinicKnowledgeManager();
        manager.init();
        window.ClinicKnowledgeManagerInstance = manager;
    });
}

export { ClinicKnowledgeManager };
