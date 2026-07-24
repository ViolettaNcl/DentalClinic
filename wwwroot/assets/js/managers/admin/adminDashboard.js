import {
    formatDate, formatDateTime, toInputDateTime, dateToString
} from '../../services/dateUtils.js'; import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, showConfirm, escapeHtml, renderPagination } from '../../services/ui.js';
import { initAvatarUploader, paintAvatarEverywhere } from '../../services/avatarService.js';
import { t } from '../../core/i18n.js';

function checkAdminAccess() {
    const role = sessionStorage.getItem('userRole');
    if (role?.toLowerCase() !== 'admin') { window.location.href = '/index.html'; return false; }
    return true;
}

function logout() { sessionStorage.clear(); window.location.href = '/index.html'; }

function initNav() {
    const navLinks = document.querySelectorAll('.panel-nav-link');
    const sections = document.querySelectorAll('.panel-section');
    const storageKey = 'admin_active_section';
    const saved = sessionStorage.getItem(storageKey);
    showSection(saved || 'requests');

    navLinks.forEach(link => {
        link.addEventListener('click', () => {
            const section = link.dataset.section;
            if (section === 'logout') return;
            showSection(section);
            sessionStorage.setItem(storageKey, section);
        });
    });

    function showSection(sectionId) {
        navLinks.forEach(l => l.classList.toggle('active', l.dataset.section === sectionId));
        sections.forEach(s => s.classList.toggle('hidden', s.id !== `section-${sectionId}`));
    }

    document.querySelectorAll('.panel-tabs').forEach(tabs => {
        const btns = tabs.querySelectorAll('.panel-tab');
        const panels = tabs.closest('.panel-card').querySelectorAll('.panel-panel');
        btns.forEach(btn => btn.addEventListener('click', () => {
            const target = btn.dataset.tab;
            btns.forEach(b => b.classList.toggle('active', b === btn));
            panels.forEach(p => p.classList.toggle('hidden', p.id.replace('panel-', '') !== target));
        }));
    });
}

async function loadDoctors() {
    const selects = {
        phone: document.getElementById('phone-doctor'),
        calendar: document.getElementById('calendar-doctor'),
        edit: document.getElementById('edit-doctor'),
    };
    if (!Object.values(selects).some(Boolean)) return;
    try {
        const doctors = await apiFetch('/doctor');
        window.DoctorsDictionary = Object.fromEntries(doctors.map(d => [d.id, d]));
        Object.values(selects).forEach(select => {
            if (!select) return;
            select.innerHTML = '<option value="">Выберите врача</option>';
            doctors.forEach(d => {
                const opt = document.createElement('option');
                opt.value = d.id; opt.textContent = d.fullName;
                select.appendChild(opt);
            });
        });
        setTimeout(() => window.DoctorCalendarManagerInstance?.refresh?.(), 100);
    } catch (err) {
        console.error('loadDoctors error:', err);
        showError('Не удалось загрузить список врачей');
        Object.values(selects).forEach(s => { if (s) s.innerHTML = '<option value="">Ошибка загрузки врачей</option>'; });
    }
}

class AdminRequestsManager {
    constructor() {
        this.tbody = {
            reqReg: document.getElementById('admin-requests-registered-body'),
            reqGuest: document.getElementById('admin-requests-guests-body'),
            reqBot: document.getElementById('admin-requests-bot-body'),
            schReg: document.getElementById('admin-schedule-registered-body'),
            schGuest: document.getElementById('admin-schedule-guests-body'),
            schBot: document.getElementById('admin-schedule-bot-body'),
            history: document.getElementById('admin-history-body'),
        };
        this.statusMap = { pending: 'Ожидание', confirmed: 'Подтверждено', completed: 'Завершено', cancelled: 'Отменено' };
        this.modal = {
            wrap: document.getElementById('edit-appointment-modal'),
            form: document.getElementById('edit-appointment-form'),
            id: document.getElementById('edit-appointment-id'),
            doctor: document.getElementById('edit-doctor'),
            datetime: document.getElementById('edit-datetime'),
            comment: document.getElementById('edit-comment'),
            close: document.getElementById('edit-modal-close'),
            cancel: document.getElementById('edit-modal-cancel'),
        };
        this._cache = [];
        this._handler = null;
        this._historyFilter = 'all';
        this._historyStatus = 'all';
        this._historySearch = '';
        this._historyPage = 1;
        this._historyPageSize = 20;
    }

    init() {
        if (!this.tbody.reqReg || !this.tbody.reqGuest) return;
        this.loadAll();
        this.modal.form?.addEventListener('submit', e => this._submitEdit(e));
        this.modal.close?.addEventListener('click', () => this._hideModal());
        this.modal.cancel?.addEventListener('click', () => this._hideModal());
        this._attachRowHandlers();
        this._initHistoryFilters();
    }

    // ── История: фильтры и поиск ──
    _initHistoryFilters() {
        // Фильтр по типу
        document.querySelectorAll('[data-filter]').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('[data-filter]').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this._historyFilter = btn.dataset.filter;
                this._historyPage = 1;
                this._renderHistory();
            });
        });
        // Фильтр по статусу
        document.querySelectorAll('[data-status]').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('[data-status]').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                this._historyStatus = btn.dataset.status;
                this._historyPage = 1;
                this._renderHistory();
            });
        });
        // Поиск
        const searchInput = document.getElementById('history-search');
        if (searchInput) {
            searchInput.addEventListener('input', () => {
                this._historySearch = searchInput.value.toLowerCase().trim();
                this._historyPage = 1;
                this._renderHistory();
            });
        }
    }

    _renderHistory() {
        const s = (r) => (r.status || '').toLowerCase();
        let history = this._cache.filter(r => ['completed', 'cancelled'].includes(s(r)));

        // Фильтр по типу
        if (this._historyFilter === 'registered') history = history.filter(r => r.patientId > 0);
        else if (this._historyFilter === 'bot') history = history.filter(r => this._isBot(r));
        else if (this._historyFilter === 'guest') history = history.filter(r => !r.patientId && !this._isBot(r));

        // Фильтр по статусу
        if (this._historyStatus !== 'all') history = history.filter(r => s(r) === this._historyStatus);

        // Поиск по имени или телефону
        if (this._historySearch) {
            history = history.filter(r =>
                (r.firstName || '').toLowerCase().includes(this._historySearch) ||
                (r.phone || '').toLowerCase().includes(this._historySearch)
            );
        }

        // Счётчик
        const countEl = document.getElementById('history-count');
        if (countEl) countEl.textContent = history.length ? `Найдено: ${history.length}` : '';

        // Пагинация — показываем только текущую страницу, чтобы не рендерить
        // разом тысячи строк в DOM, если история накопится за долгое время.
        const totalPages = Math.max(1, Math.ceil(history.length / this._historyPageSize));
        if (this._historyPage > totalPages) this._historyPage = totalPages;
        const start = (this._historyPage - 1) * this._historyPageSize;
        const pageItems = history.slice(start, start + this._historyPageSize);

        this._fill(this.tbody.history, pageItems.map(r => this._rowHistory(r)), 'Записей не найдено', 7);

        renderPagination(document.getElementById('history-pagination'), {
            page: this._historyPage,
            totalItems: history.length,
            pageSize: this._historyPageSize,
            onPageChange: (p) => { this._historyPage = p; this._renderHistory(); }
        });
    }

    _msg(message) {
        const cfg = [
            { el: this.tbody.reqReg, cols: 7 },
            { el: this.tbody.reqGuest, cols: 7 },
            { el: this.tbody.reqBot, cols: 7 },
            { el: this.tbody.schReg, cols: 8 },
            { el: this.tbody.schGuest, cols: 8 },
            { el: this.tbody.schBot, cols: 8 },
            { el: this.tbody.history, cols: 7 },
        ];
        cfg.forEach(({ el, cols }) => { if (el) el.innerHTML = `<tr><td colspan="${cols}">${message}</td></tr>`; });
    }

    _fill(tbody, rows, empty, cols) {
        if (!tbody) return;
        tbody.innerHTML = rows.length ? rows.join('') : `<tr><td colspan="${cols}">${empty}</td></tr>`;
    }

    _byType(list) {
        return {
            reg: list.filter(r => r.patientId > 0),
            guest: list.filter(r => !r.patientId || r.patientId <= 0),
        };
    }

    _isBot(r) { return (r.comment || '').includes('[Заявка через чат]'); }

    async loadAll() {
        this._msg('Загрузка...');
        try {
            const all = await apiFetch('/appointmentrequest/admin/all');
            this._cache = all;

            const s = (r) => (r.status || '').toLowerCase();
            const pending = all.filter(r => s(r) === 'pending');
            const confirmed = all.filter(r => s(r) === 'confirmed');

            const { reg: rP, guest: gP } = this._byType(pending);
            const { reg: rC, guest: gC } = this._byType(confirmed);

            const gPRegular = gP.filter(r => !this._isBot(r));
            const gPBot = gP.filter(r => this._isBot(r));
            const gCRegular = gC.filter(r => !this._isBot(r));
            const gCBot = gC.filter(r => this._isBot(r));

            this._fill(this.tbody.reqReg, rP.map(r => this._rowRequest(r)), 'Новых заявок от пользователей нет', 7);
            this._fill(this.tbody.reqGuest, gPRegular.map(r => this._rowRequest(r)), 'Новых гостевых заявок нет', 7);
            this._fill(this.tbody.reqBot, gPBot.map(r => this._rowBot(r)), 'Заявок через AI-чат нет', 7);
            this._fill(this.tbody.schReg, rC.map(r => this._rowSchedule(r)), 'Нет подтверждённых записей от пользователей', 8);
            this._fill(this.tbody.schGuest, gCRegular.map(r => this._rowSchedule(r)), 'Нет подтверждённых записей от гостей', 8);
            this._fill(this.tbody.schBot, gCBot.map(r => this._rowScheduleBot(r)), 'Подтверждённых заявок через бот нет', 8);

            // История рендерится через фильтр
            this._renderHistory();

            // Аналитика подписана на те же данные — обновляем при каждой загрузке
            window.AnalyticsManagerInstance?.setData(all);
        } catch (err) {
            console.error('loadAll error:', err);
            this._msg('Ошибка загрузки');
            showError('Не удалось загрузить данные');
        }
    }

    _doctor(id) { if (!id) return '—'; return (window.DoctorsDictionary?.[id])?.fullName ?? '—'; }
    _status(r) { return (r.status || '').toLowerCase(); }

    _rowRequest(r) {
        return `<tr data-id="${r.id}">
                <td>${r.id}</td><td>${escapeHtml(r.firstName) || '-'}</td><td>${escapeHtml(r.phone)}</td>
                <td>${formatDate(r.appointmentDate)}</td><td>${escapeHtml(r.comment) || '—'}</td>
                <td><span class="status-badge status-pending">${this.statusMap[this._status(r)] || r.status}</span></td>
                <td><div class="panel-table-actions">
                  <button class="btn-tag btn-edit"    data-action="edit"    title="Редактировать">✏️</button>
                  <button class="btn-tag btn-confirm" data-action="confirm" title="Подтвердить">✓</button>
                  <button class="btn-tag btn-cancel"  data-action="cancel"  title="Отклонить">✕</button>
                </div></td></tr>`;
    }

    _rowBot(r) {
        const c = escapeHtml((r.comment || '').replace('[Заявка через чат]', '').trim().replace(/\.$/, '') || '—');
        return `<tr data-id="${r.id}" class="row-bot">
                <td>${r.id} <span class="bot-badge" title="AI-чат">🤖</span></td>
                <td>${escapeHtml(r.firstName) || '-'}</td><td>${escapeHtml(r.phone)}</td>
                <td>${formatDate(r.appointmentDate)}</td><td>${c}</td>
                <td><span class="status-badge status-pending">${this.statusMap[this._status(r)] || r.status}</span></td>
                <td><div class="panel-table-actions">
                  <button class="btn-tag btn-edit"    data-action="edit"    title="Редактировать">✏️</button>
                  <button class="btn-tag btn-confirm" data-action="confirm" title="Подтвердить">✓</button>
                  <button class="btn-tag btn-cancel"  data-action="cancel"  title="Отклонить">✕</button>
                </div></td></tr>`;
    }

    _rowSchedule(r) {
        return `<tr data-id="${r.id}">
                <td>${r.id}</td><td>${escapeHtml(r.firstName) || '-'}</td><td>${this._doctor(r.doctorId)}</td>
                <td>${formatDateTime(r.appointmentDate)}</td><td>${escapeHtml(r.phone)}</td><td>${escapeHtml(r.comment) || '—'}</td>
                <td><span class="status-badge status-confirmed">${this.statusMap[this._status(r)] || r.status}</span></td>
                <td><div class="panel-table-actions">
                  <button class="btn-tag btn-edit"     data-action="edit"     title="Редактировать">✏️</button>
                  <button class="btn-tag btn-complete" data-action="complete" title="Завершить">✓</button>
                  <button class="btn-tag btn-cancel"   data-action="cancel"   title="Отменить">✕</button>
                </div></td></tr>`;
    }

    _rowScheduleBot(r) {
        const c = escapeHtml((r.comment || '').replace('[Заявка через чат]', '').trim().replace(/\.$/, '') || '—');
        return `<tr data-id="${r.id}" class="row-bot">
                <td>${r.id} <span class="bot-badge" title="AI-чат">🤖</span></td>
                <td>${escapeHtml(r.firstName) || '-'}</td><td>${this._doctor(r.doctorId)}</td>
                <td>${formatDateTime(r.appointmentDate)}</td><td>${escapeHtml(r.phone)}</td><td>${c}</td>
                <td><span class="status-badge status-confirmed">${this.statusMap[this._status(r)] || r.status}</span></td>
                <td><div class="panel-table-actions">
                  <button class="btn-tag btn-edit"     data-action="edit"     title="Редактировать">✏️</button>
                  <button class="btn-tag btn-complete" data-action="complete" title="Завершить">✓</button>
                  <button class="btn-tag btn-cancel"   data-action="cancel"   title="Отменить">✕</button>
                </div></td></tr>`;
    }

    _rowHistory(r) {
        const cls = this._status(r) === 'completed' ? 'status-completed' : 'status-cancelled';
        const type = r.patientId > 0 ? '<span class="type-badge type-reg">👤</span>'
            : this._isBot(r) ? '<span class="type-badge type-bot">🤖</span>'
                : '<span class="type-badge type-guest">🧑</span>';
        return `<tr data-id="${r.id}">
                <td>${r.id}</td><td>${escapeHtml(r.firstName) || '-'}</td><td>${type}</td>
                <td>${this._doctor(r.doctorId)}</td>
                <td>${formatDateTime(r.appointmentDate)}</td><td>${escapeHtml(r.phone)}</td>
                <td><span class="status-badge ${cls}">${this.statusMap[this._status(r)] || r.status}</span></td>
              </tr>`;
    }

    _attachRowHandlers() {
        // Слушаем клики только внутри таблиц заявок этого менеджера, а не всего
        // контейнера панели — иначе клики в других разделах (например, "Врачи",
        // у которого тоже есть кнопки data-action="edit") случайно перехватывались
        // бы этим же обработчиком и открывали не то модальное окно.
        const bodies = Object.values(this.tbody).filter(Boolean);
        if (this._handler) {
            bodies.forEach(body => body.removeEventListener('click', this._handler));
        }
        this._handler = async ({ target }) => {
            const btn = target.closest('button[data-action]');
            if (!btn) return;
            const id = btn.closest('tr')?.dataset.id;
            if (!id) return;
            const { action } = btn.dataset;
            if (action === 'edit') { await this._openEdit(id); return; }
            const map = { confirm: 'confirmed', cancel: 'cancelled', complete: 'completed' };
            if (map[action]) await this._changeStatus(id, map[action]);
        };
        bodies.forEach(body => body.addEventListener('click', this._handler));
    }

    _showModal() { this.modal.wrap?.classList.remove('hidden'); }
    _hideModal() { this.modal.wrap?.classList.add('hidden'); }

    async _openEdit(id) {
        const { wrap, doctor, datetime, comment } = this.modal;
        if (!wrap || !doctor || !datetime || !comment) { showError('Модальное окно не настроено'); return; }
        const rec = this._cache.find(r => r.id === Number(id));
        this.modal.id.value = id;
        comment.value = rec?.comment || '';
        datetime.value = toInputDateTime(rec?.appointmentDate);
        doctor.value = String(rec?.doctorId ?? rec?.DoctorId ?? '');
        this._showModal();
    }

    async _submitEdit(e) {
        e.preventDefault();
        const id = this.modal.id.value;
        if (!id) { showError('Не найдена запись'); return; }
        const raw = this.modal.datetime?.value || null;
        const payload = {
            doctorId: parseInt(this.modal.doctor?.value, 10) || null,
            comment: this.modal.comment.value.trim() || null,
            appointmentDate: raw ? (raw.length === 16 ? `${raw}:00` : raw) : null,
        };
        try {
            await apiFetch(`/appointmentrequest/${id}`, { method: 'PUT', body: JSON.stringify(payload) });
            this._hideModal(); await this.loadAll(); showSuccess('Изменения сохранены');
        } catch (err) { showError('Ошибка сохранения: ' + err.message); }
    }

    async _changeStatus(id, status) {
        try {
            await apiFetch(`/appointmentrequest/${id}`, { method: 'PUT', body: JSON.stringify({ status }) });
            await this.loadAll(); showSuccess('Статус обновлён');
        } catch (err) { showError('Ошибка изменения статуса: ' + err.message); }
    }
}

function initAdminProfile() {
    const emailEl = document.getElementById('admin-profile-email');
    const createdEl = document.getElementById('admin-profile-created');
    if (!emailEl && !createdEl) return;

    apiFetch('/auth/admin/profile')
        .then(profile => {
            if (emailEl) emailEl.value = profile.email || '';
            if (createdEl) createdEl.value = profile.createdAt ? formatDate(profile.createdAt) : '';

            paintAvatarEverywhere(profile.avatarUrl);
            initAvatarUploader({
                rootId: 'admin-profile-avatar-uploader',
                initialUrl: profile.avatarUrl,
                fallbackIcon: '👤'
            });
        })
        .catch(err => {
            console.error('initAdminProfile error:', err);
            showError('Не удалось загрузить данные профиля');
        });
}

function initPhoneForm() {
    const form = document.getElementById('phone-appointment-form');
    if (!form) return;
    const val = id => document.getElementById(id)?.value.trim() ?? '';

    // Чипы быстрого выбора даты
    const dateInput = document.getElementById('phone-date');
    document.querySelectorAll('#phone-date-chips .quickpick-chip').forEach(chip => {
        chip.addEventListener('click', () => {
            const d = new Date();
            d.setDate(d.getDate() + parseInt(chip.dataset.offset, 10));
            dateInput.value = dateToString(d);
            document.querySelectorAll('#phone-date-chips .quickpick-chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');
        });
    });

    // Чипы быстрого выбора времени
    const timeInput = document.getElementById('phone-time');
    document.querySelectorAll('#phone-time-chips .quickpick-chip').forEach(chip => {
        chip.addEventListener('click', () => {
            timeInput.value = chip.dataset.time;
            document.querySelectorAll('#phone-time-chips .quickpick-chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');
        });
    });

    form.addEventListener('submit', async e => {
        e.preventDefault();
        const name = val('phone-name'), phone = val('phone-phone');
        const date = val('phone-date'), time = val('phone-time');
        const comment = val('phone-comment');
        const doctorId = parseInt(val('phone-doctor'), 10) || null;
        if (!name || !phone) { showError('Имя и телефон обязательны'); return; }
        const appointmentDate = date ? `${date}T${time || '00:00'}:00` : null;
        try {
            await apiFetch('/appointmentrequest/admin/phone', {
                method: 'POST', body: JSON.stringify({ firstName: name, phone, comment: comment || null, appointmentDate, doctorId }),
            });
            showSuccess('Запись по телефону сохранена'); form.reset();
            document.querySelectorAll('.phone-quickpick-card .quickpick-chip.active').forEach(c => c.classList.remove('active'));
            window.AdminRequestsManagerInstance?.loadAll();
        } catch (err) { showError('Ошибка создания записи: ' + (err.message || 'неизвестная ошибка')); }
    });
}

class DoctorCalendarManager {
    constructor() {
        this.select = document.getElementById('calendar-doctor');
        this.weekInput = document.getElementById('calendar-week');
        this.tbody = document.getElementById('calendar-slots-body');
        this.slots = ['09:00', '11:00', '13:00', '15:00', '17:00', '19:00'];
    }
    init() {
        if (!this.select || !this.weekInput || !this.tbody) return;
        this.weekInput.value = dateToString(this._monday(new Date()));
        this.select.addEventListener('change', () => this.refresh());
        this.weekInput.addEventListener('change', () => this.refresh());
        if (this.select.options.length > 1) this.refresh();
    }
    _monday(date) {
        const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        const diff = (d.getDay() === 0 ? -6 : 1) - d.getDay();
        d.setDate(d.getDate() + diff); return d;
    }
    _weekRange() {
        const mon = this._monday(this.weekInput.value ? new Date(this.weekInput.value) : new Date());
        const sun = new Date(mon); sun.setDate(sun.getDate() + 6);
        return { from: dateToString(mon), to: dateToString(sun) };
    }
    async refresh() {
        const doctorId = parseInt(this.select.value, 10);
        if (!doctorId) { this.tbody.innerHTML = '<tr><td colspan="7">Выберите врача</td></tr>'; return; }
        this.tbody.innerHTML = '<tr><td colspan="7">Загрузка...</td></tr>';
        const { from, to } = this._weekRange();
        try {
            const data = await apiFetch(`/doctorschedule?doctorId=${doctorId}&from=${from}&to=${to}`);
            const busyMap = {};
            data.forEach(a => { const [dp, tp] = String(a.appointmentDate || '').split('T'); if (dp && tp) busyMap[`${dp}|${tp.slice(0, 5)}`] = true; });
            this._render(from, busyMap);
        } catch { showError('Ошибка загрузки расписания'); this.tbody.innerHTML = '<tr><td colspan="7">Ошибка загрузки</td></tr>'; }
    }
    _render(weekStr, busyMap) {
        const headers = document.querySelectorAll('.panel-table-calendar thead tr th');
        const [y, m, d] = weekStr.split('-').map(Number);
        const mon = new Date(y, m - 1, d);
        for (let i = 0; i < 6; i++) {
            const cell = headers[i + 1]; if (!cell) continue;
            const dt = new Date(mon); dt.setDate(dt.getDate() + i);
            const base = cell.dataset.label || cell.textContent.trim(); cell.dataset.label = base;
            cell.textContent = `${base} ${String(dt.getDate()).padStart(2, '0')}.${String(dt.getMonth() + 1).padStart(2, '0')}`;
        }
        this.tbody.innerHTML = this.slots.map(time => {
            let row = `<tr><td>${time}</td>`;
            for (let i = 0; i < 6; i++) {
                const dt = new Date(mon); dt.setDate(dt.getDate() + i);
                const busy = !!busyMap[`${dateToString(dt)}|${time}`];
                row += `<td><span class="slot ${busy ? 'slot-busy' : 'slot-free'}">${busy ? 'Запись' : 'Свободно'}</span></td>`;
            }
            return row + '</tr>';
        }).join('');
    }
}

/* =====================================================
   АНАЛИТИКА (Analytics Dashboard)
   Строит карточки-цифры и графики Chart.js на основе
   тех же данных, что и AdminRequestsManager.
===================================================== */
class AnalyticsManager {
    constructor() {
        this.section = document.getElementById('section-analytics');
        this.els = {
            total: document.getElementById('an-total'),
            month: document.getElementById('an-month'),
            confirmedRate: document.getElementById('an-confirmed-rate'),
            pending: document.getElementById('an-pending'),
        };
        this.reviewEls = {
            total: document.getElementById('an-rev-total'),
            avg: document.getElementById('an-rev-avg'),
            pending: document.getElementById('an-rev-pending'),
            rejected: document.getElementById('an-rev-rejected'),
        };
        this.canvases = {
            byDay: document.getElementById('chart-by-day'),
            source: document.getElementById('chart-source'),
            doctors: document.getElementById('chart-doctors'),
            reviewsRating: document.getElementById('chart-reviews-rating'),
        };
        this.charts = {};
        this._data = null;
        this._reviewData = null;
        this._chatStats = null;
        this._chatSessions = null;
        this._rendered = false;
    }

    init() {
        if (!this.section) return;
        const navBtn = document.querySelector('.panel-nav-link[data-section="analytics"]');
        // Строим графики, когда пользователь реально открыл вкладку
        // (Chart.js не умеет рисовать в canvas со скрытым родителем)
        navBtn?.addEventListener('click', () => this._tryRender());
        if (sessionStorage.getItem('admin_active_section') === 'analytics') this._tryRender();
        this.loadReviews();
        this.loadChatAnalytics();
    }

    // AI-чат «Дента»: сколько вопросов задают и о чём чаще всего спрашивают —
    // прямая подсказка админу какие услуги продвигать активнее.
    async loadChatAnalytics() {
        try {
            const [stats, sessions] = await Promise.all([
                apiFetch('/chat/admin/stats?days=30'),
                apiFetch('/chat/admin/sessions?take=30'),
            ]);
            this._chatStats = stats;
            this._chatSessions = sessions;
            this._renderChatCards();
            this._renderChatSessionsTable();
            this._tryRender();
        } catch (err) {
            console.error('AnalyticsManager loadChatAnalytics error:', err);
            const tbody = document.getElementById('chat-sessions-tbody');
            if (tbody) tbody.innerHTML = '<tr><td colspan="3">Не удалось загрузить данные чата</td></tr>';
        }
    }

    _renderChatCards() {
        if (!this._chatStats) return;
        const elMsgs = document.getElementById('an-chat-messages');
        const elSessions = document.getElementById('an-chat-sessions');
        if (elMsgs) elMsgs.textContent = this._chatStats.totalMessages ?? 0;
        if (elSessions) elSessions.textContent = this._chatStats.totalSessions ?? 0;
    }

    _renderChatTopicsChart() {
        const ctx = document.getElementById('chart-chat-topics');
        if (!ctx || !this._chatStats) return;
        const topics = this._chatStats.topics || [];

        this.charts.chatTopics?.destroy();

        if (!topics.length) {
            const wrap = ctx.closest('.analytics-chart-wrap');
            if (wrap) wrap.innerHTML = '<div class="analytics-chart-empty">Пока недостаточно данных по чату</div>';
            return;
        }

        this.charts.chatTopics = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: topics.map(t => t.topic),
                datasets: [{
                    label: 'Вопросов',
                    data: topics.map(t => t.count),
                    backgroundColor: '#0a8a77',
                    hoverBackgroundColor: '#13b39b',
                    borderRadius: 6,
                    maxBarThickness: 40,
                }],
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: '#eef3f1' } },
                    y: { grid: { display: false }, ticks: { font: { size: 12 } } },
                },
            },
        });
    }

    _renderChatSessionsTable() {
        const tbody = document.getElementById('chat-sessions-tbody');
        if (!tbody) return;
        const sessions = this._chatSessions || [];

        if (!sessions.length) {
            tbody.innerHTML = '<tr><td colspan="3">Пока нет сообщений в чате</td></tr>';
            return;
        }

        tbody.innerHTML = '';
        sessions.forEach(s => {
            const row = document.createElement('tr');
            row.className = 'chat-session-row';
            row.style.cursor = 'pointer';
            const date = new Date(s.startedAt).toLocaleString('ru');
            row.innerHTML = `
                <td>${date}</td>
                <td>${s.messageCount}</td>
                <td>${this._escapeHtml(s.preview || '')}</td>`;

            const detail = document.createElement('tr');
            detail.className = 'chat-session-detail hidden';
            const messagesHtml = (s.messages || [])
                .map(m => `<div><strong>${m.role === 'user' ? '🧑 Пациент' : '🦷 Дента'}:</strong> ${this._escapeHtml(m.text)}</div>`)
                .join('');
            detail.innerHTML = `<td colspan="3"><div class="chat-session-messages">${messagesHtml}</div></td>`;

            row.addEventListener('click', () => detail.classList.toggle('hidden'));

            tbody.appendChild(row);
            tbody.appendChild(detail);
        });
    }

    _escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text ?? '';
        return div.innerHTML;
    }

    // Отзывы для аналитики загружаются отдельно от заявок (свой набор эндпоинтов),
    // поэтому у них собственный метод загрузки, а не setData() снаружи.
    async loadReviews() {
        try {
            const [pending, approved, rejected] = await Promise.all([
                apiFetch('/review/admin/pending'),
                apiFetch('/review/admin/approved'),
                apiFetch('/review/admin/rejected'),
            ]);
            this._reviewData = { pending, approved, rejected };
            this._renderReviewCards();
            this._tryRender();
        } catch (err) {
            console.error('AnalyticsManager loadReviews error:', err);
        }
    }

    _renderReviewCards() {
        if (!this._reviewData) return;
        const { pending, approved, rejected } = this._reviewData;
        const total = pending.length + approved.length + rejected.length;
        const avg = approved.length
            ? (approved.reduce((s, r) => s + r.rating, 0) / approved.length).toFixed(1)
            : '—';

        if (this.reviewEls.total) this.reviewEls.total.textContent = total;
        if (this.reviewEls.avg) this.reviewEls.avg.textContent = approved.length ? `${avg} ★` : '—';
        if (this.reviewEls.pending) this.reviewEls.pending.textContent = pending.length;
        if (this.reviewEls.rejected) this.reviewEls.rejected.textContent = rejected.length;
    }

    _renderReviewsRatingChart() {
        const ctx = this.canvases.reviewsRating;
        if (!ctx || !this._reviewData) return;
        const { approved } = this._reviewData;

        const counts = [1, 2, 3, 4, 5].map(star => approved.filter(r => r.rating === star).length);

        this.charts.reviewsRating?.destroy();

        if (!approved.length) {
            const wrap = ctx.closest('.analytics-chart-wrap');
            if (wrap) wrap.innerHTML = '<div class="analytics-chart-empty">Пока нет опубликованных отзывов</div>';
            return;
        }

        this.charts.reviewsRating = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: ['1 ★', '2 ★', '3 ★', '4 ★', '5 ★'],
                datasets: [{
                    label: 'Отзывов',
                    data: counts,
                    backgroundColor: '#13b39b',
                    hoverBackgroundColor: '#0a8a77',
                    borderRadius: 6,
                    maxBarThickness: 46,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { grid: { display: false }, ticks: { font: { size: 12 } } },
                    y: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } }, grid: { color: '#eef3f1' } },
                },
            },
        });
    }

    // Вызывается из AdminRequestsManager.loadAll() при каждом обновлении данных
    setData(all) {
        this._data = all || [];
        this._renderCards();
        this._tryRender();
    }

    _tryRender() {
        if (!this._data && !this._reviewData && !this._chatStats) return;
        if (typeof Chart === 'undefined') {
            // Chart.js подключён с defer — на случай если ещё не готов, подождём немного
            setTimeout(() => this._tryRender(), 150);
            return;
        }
        if (this._data) {
            this._renderByDayChart();
            this._renderSourceChart();
            this._renderDoctorsChart();
        }
        if (this._reviewData) {
            this._renderReviewsRatingChart();
        }
        if (this._chatStats) {
            this._renderChatTopicsChart();
        }
    }

    _isBot(r) { return (r.comment || '').includes('[Заявка через чат]'); }

    _renderCards() {
        const data = this._data;
        const total = data.length;

        const now = new Date();
        const monthCount = data.filter(r => {
            const d = new Date(r.appointmentDate);
            return !Number.isNaN(d.getTime()) && d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
        }).length;

        const confirmedLike = data.filter(r => ['confirmed', 'completed'].includes((r.status || '').toLowerCase())).length;
        const rate = total ? Math.round((confirmedLike / total) * 100) : 0;
        const pending = data.filter(r => (r.status || '').toLowerCase() === 'pending').length;

        if (this.els.total) this.els.total.textContent = total;
        if (this.els.month) this.els.month.textContent = monthCount;
        if (this.els.confirmedRate) this.els.confirmedRate.textContent = `${rate}%`;
        if (this.els.pending) this.els.pending.textContent = pending;
    }

    _renderByDayChart() {
        const ctx = this.canvases.byDay;
        if (!ctx) return;
        const data = this._data;

        const days = [];
        const counts = {};
        const today = new Date();
        for (let i = 29; i >= 0; i--) {
            const d = new Date(today.getFullYear(), today.getMonth(), today.getDate() - i);
            const key = dateToString(d);
            days.push(key);
            counts[key] = 0;
        }
        data.forEach(r => {
            const key = String(r.appointmentDate || '').slice(0, 10);
            if (key in counts) counts[key]++;
        });
        const labels = days.map(k => { const [, m, d] = k.split('-'); return `${d}.${m}`; });

        this.charts.byDay?.destroy();
        this.charts.byDay = new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [{
                    label: 'Заявки',
                    data: days.map(k => counts[k]),
                    borderColor: '#13b39b',
                    backgroundColor: 'rgba(19,179,155,0.14)',
                    fill: true,
                    tension: 0.35,
                    pointRadius: 2,
                    pointHoverRadius: 5,
                    pointBackgroundColor: '#0a8a77',
                    borderWidth: 2,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { grid: { display: false }, ticks: { maxTicksLimit: 10, font: { size: 11 } } },
                    y: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } }, grid: { color: '#eef3f1' } },
                },
            },
        });
    }

    _renderSourceChart() {
        const ctx = this.canvases.source;
        if (!ctx) return;
        const data = this._data;

        const bot = data.filter(r => this._isBot(r)).length;
        const guest = data.filter(r => !r.patientId && !this._isBot(r)).length;
        const user = data.filter(r => r.patientId > 0).length;

        this.charts.source?.destroy();
        this.charts.source = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Пользователи', 'Гости', 'Через бота'],
                datasets: [{
                    data: [user, guest, bot],
                    backgroundColor: ['#13b39b', '#5fd9c4', '#0a6fad'],
                    borderWidth: 2,
                    borderColor: '#ffffff',
                    hoverOffset: 6,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '62%',
                plugins: {
                    legend: { position: 'bottom', labels: { font: { size: 12 }, padding: 12, boxWidth: 12 } },
                },
            },
        });
    }

    _renderDoctorsChart() {
        const ctx = this.canvases.doctors;
        if (!ctx) return;
        const data = this._data;

        const counts = {};
        data.forEach(r => {
            if (!r.doctorId) return;
            counts[r.doctorId] = (counts[r.doctorId] || 0) + 1;
        });
        const entries = Object.entries(counts)
            .map(([id, count]) => ({ name: window.DoctorsDictionary?.[id]?.fullName || `Врач #${id}`, count }))
            .sort((a, b) => b.count - a.count)
            .slice(0, 8);

        this.charts.doctors?.destroy();

        if (!entries.length) {
            const wrap = ctx.closest('.analytics-chart-wrap');
            if (wrap) wrap.innerHTML = '<div class="analytics-chart-empty">Нет данных по врачам</div>';
            return;
        }

        this.charts.doctors = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: entries.map(e => e.name),
                datasets: [{
                    label: 'Записей',
                    data: entries.map(e => e.count),
                    backgroundColor: '#13b39b',
                    hoverBackgroundColor: '#0a8a77',
                    borderRadius: 6,
                    maxBarThickness: 28,
                }],
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } }, grid: { color: '#eef3f1' } },
                    y: { grid: { display: false }, ticks: { font: { size: 11 } } },
                },
            },
        });
    }
}

document.addEventListener('DOMContentLoaded', async () => {
    if (!checkAdminAccess()) return;
    initNav();
    const logoutBtn = document.getElementById('btn-logout');
    if (logoutBtn) logoutBtn.addEventListener('click', async e => {
        e.preventDefault();
        const ok = await showConfirm(t('auth_logout_confirm_admin_text', 'Вы уверены, что хотите выйти из панели администратора?'), {
            title: t('auth_logout_confirm_title', 'Выход из аккаунта'),
            confirmText: t('auth_logout_confirm_yes', 'Да, выйти'),
            cancelText: t('auth_logout_confirm_stay', 'Остаться'),
            danger: true,
            icon: '🚪'
        });
        if (ok) logout();
    });
    window.reloadDoctorSelects = loadDoctors;
    await loadDoctors();
    const calendar = new DoctorCalendarManager(); calendar.init(); window.DoctorCalendarManagerInstance = calendar;
    const analytics = new AnalyticsManager(); analytics.init(); window.AnalyticsManagerInstance = analytics;
    const requests = new AdminRequestsManager(); requests.init(); window.AdminRequestsManagerInstance = requests;
    initPhoneForm();
    initAdminProfile();
    initExportButtons();
});

/* =====================================================
   ЭКСПОРТ: кнопки "Скачать Excel" / "Печать / PDF" во вкладке "Аналитика"
===================================================== */
function initExportButtons() {
    const section = document.getElementById('section-analytics');
    const header = section ? section.querySelector('.panel-section-header') : null;
    if (!header || document.getElementById('analytics-export-bar')) return;

    const bar = document.createElement('div');
    bar.id = 'analytics-export-bar';
    bar.className = 'analytics-export-bar';
    bar.innerHTML = `
        <span class="analytics-export-bar-label">
            <span class="analytics-export-bar-label-icon">📦</span>
            Экспорт отчётов
        </span>
        <button type="button" id="btn-export-xlsx" class="btn-export btn-export-primary">
            <span class="btn-export-icon">📊</span> Скачать Excel
        </button>
        <button type="button" id="btn-export-pdf" class="btn-export btn-export-secondary">
            <span class="btn-export-icon">🖨️</span> Печать / PDF
        </button>
    `;
    header.insertAdjacentElement('afterend', bar);

    const token = () => sessionStorage.getItem('authToken') || '';

    document.getElementById('btn-export-xlsx').addEventListener('click', async () => {
        const btn = document.getElementById('btn-export-xlsx');
        btn.disabled = true;
        try {
            const res = await fetch('/api/adminstats/export/xlsx', {
                headers: { Authorization: `Bearer ${token()}` }
            });
            if (!res.ok) throw new Error('Не удалось сформировать файл');
            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `zayavki_${new Date().toISOString().slice(0, 10)}.xlsx`;
            a.click();
            URL.revokeObjectURL(url);
            showSuccess('Excel-файл сформирован');
        } catch (err) {
            showError(err.message || 'Не удалось скачать Excel-файл');
        } finally {
            btn.disabled = false;
        }
    });

    document.getElementById('btn-export-pdf').addEventListener('click', async () => {
        // Печатный отчёт открываем в новой вкладке. Токен передаём через заголовок
        // Authorization (обычный fetch), а не через ?access_token= в URL — так
        // умеет аутентифицироваться только SignalR-хаб (/hubs/notifications),
        // остальные контроллеры читают токен из заголовка.
        //
        // Окно открываем СРАЗУ, синхронно по клику — иначе Safari и другие строгие
        // блокировщики всплывающих окон блокируют window.open(), если он вызван
        // после await (клик уже "устарел" как источник жеста пользователя).
        const btn = document.getElementById('btn-export-pdf');
        const win = window.open('', '_blank');
        if (!win) { showError('Браузер заблокировал всплывающее окно — разрешите всплывающие окна для этого сайта'); return; }
        win.document.write('<p style="font-family:sans-serif;padding:20px;">Формирование отчёта…</p>');
        btn.disabled = true;
        try {
            const res = await fetch('/api/adminstats/export/report', {
                headers: { Authorization: `Bearer ${token()}` }
            });
            if (!res.ok) throw new Error('Не удалось сформировать отчёт');
            const html = await res.text();
            win.document.open();
            win.document.write(html);
            win.document.close();
        } catch (err) {
            win.close();
            showError(err.message || 'Не удалось открыть отчёт');
        } finally {
            btn.disabled = false;
        }
    });
}