import { apiFetch } from '../../services/apiClient.js';
import { showError, showSuccess, showConfirm, escapeHtml } from '../../services/ui.js';
import { initAvatarUploader, paintAvatarEverywhere } from '../../services/avatarService.js';
import { formatDate, formatTime, toInputDateTime } from '../../services/dateUtils.js';
import { TabManager } from '../../core/tabManager.js';
import { t, onLanguageChange, getLang } from '../../core/i18n.js';
import { translateText } from '../../services/textTranslate.js';

class CabinetManager {
    constructor() {
        this.patientId = sessionStorage.getItem('patientId');
        this._data = [];
        this._loaded = false;
        // Резервные переводы имени врача из базы (Doctor.FullNameEn/Fr/El/Ar),
        // загружаются один раз в init() через _loadDoctorTranslations().
        // Ключ — id врача, значение — { en, fr, el, ar }.
        this._doctorTranslations = new Map();

        this._buildStatusMap();
        // При смене языка пересобираем подписи статусов и перерисовываем уже
        // загруженные записи, чтобы таблицы не остались на предыдущем языке.
        onLanguageChange(() => {
            this._buildStatusMap();
            if (this._loaded) this._renderAppointments(this._data);
        });

        this.rescheduleModal = {
            wrap: document.getElementById('reschedule-modal'),
            form: document.getElementById('reschedule-form'),
            id: document.getElementById('reschedule-appointment-id'),
            datetime: document.getElementById('reschedule-datetime'),
            close: document.getElementById('reschedule-modal-close'),
            cancel: document.getElementById('reschedule-modal-cancel'),
        };

        this.profileForm = document.getElementById('profile-form');
        this.passwordForm = document.getElementById('password-form');
    }

    _buildStatusMap() {
        this.statusMap = {
            pending: { label: t('status_pending', 'Ожидание'), cls: 'status-pending' },
            confirmed: { label: t('status_confirmed', 'Подтверждено'), cls: 'status-confirmed' },
            completed: { label: t('status_completed', 'Завершено'), cls: 'status-completed' },
            cancelled: { label: t('status_cancelled', 'Отменено'), cls: 'status-cancelled' },
        };
    }

    init() {
        // Защита уже в HTML, но оставим на случай прямого захода
        if (!this.patientId) {
            window.location.href = '/index.html';
            return;
        }

        const set = (id, key) => {
            const el = document.getElementById(id);
            if (el) el.textContent = sessionStorage.getItem(key) || '—';
        };
        set('patient-name', 'patientName');
        set('patient-email', 'patientEmail');

        const tabManager = new TabManager({
            navSelector: '.panel-nav-link',
            sectionSelector: '.panel-section',
            defaultSection: 'active',
        });
        tabManager.init();

        this._setupRescheduleModal();
        this._setupProfileForms();

        // Не блокируем отрисовку записей ожиданием этого запроса — если он
        // придёт позже первого рендера, просто перерисуем таблицы, когда
        // словарь переводов из базы будет готов.
        this._loadDoctorTranslations().then(() => {
            if (this._loaded) this._renderAppointments(this._data);
        });

        this.loadAppointments();
        this.loadProfile();
    }

    // Резервный вариант перевода имени врача — берётся напрямую из базы
    // (поля FullNameEn/FullNameFr/FullNameEl/FullNameAr в Doctor), а не через
    // внешний переводческий API. Работает мгновенно и не зависит от того,
    // поддерживает ли внешний сервис нужный язык (например, французский) и
    // не тормозит ли он в моменте.
    async _loadDoctorTranslations() {
        try {
            const doctors = await apiFetch('/doctor');
            this._doctorTranslations = new Map(
                (doctors || []).map(d => [d.id, {
                    en: d.fullNameEn || null,
                    fr: d.fullNameFr || null,
                    el: d.fullNameEl || null,
                    ar: d.fullNameAr || null,
                }])
            );
        } catch (err) {
            console.error('loadDoctorTranslations error:', err);
            // Не критично — просто останемся без "страховки" из базы и будем
            // переводить только через внешний API, как раньше.
            this._doctorTranslations = new Map();
        }
    }

    async loadAppointments() {
        try {
            const data = await apiFetch(`/appointmentrequest/patient/${this.patientId}`);
            this._data = data;
            this._renderAppointments(data);
            this._loaded = true;
        } catch (err) {
            console.error('loadAppointments error:', err);
            showError(t('patient_load_appointments_error', 'Не удалось загрузить ваши записи'));
            const activeEl = document.getElementById('active-appointments');
            if (activeEl) activeEl.innerHTML = `<tr><td colspan="6" class="panel-error">${t('ui_load_error', 'Ошибка загрузки')}</td></tr>`;
            const histEl = document.getElementById('history-appointments');
            if (histEl) histEl.innerHTML = `<tr><td colspan="5" class="panel-error">${t('ui_load_error', 'Ошибка загрузки')}</td></tr>`;
        }
    }

    // Отрисовка таблиц из уже загруженных данных — используется как после
    // запроса к серверу, так и при смене языка (без повторного похода в API).
    _renderAppointments(data) {
        const active = data.filter(a => ['pending', 'confirmed'].includes(a.status));
        const history = data.filter(a => ['completed', 'cancelled'].includes(a.status));

        this._renderTable('active-appointments', active, t('patient_no_active', 'Активных записей нет'), true);
        this._renderTable('history-appointments', history, t('patient_history_empty', 'История пуста'), false);
        this._renderHistoryStats(data, history);

        // Автоматический перевод — только для имени врача (запасной вариант,
        // если в базе нет перевода на нужный язык, см. doctorCell выше).
        // Комментарии переводятся по кнопке "🌐" в строке — это надёжнее для
        // свободного текста, у которого нет перевода в базе (см.
        // _wireCommentTranslateButtons).
        this._autoTranslateDoctorNames();
    }

    // Переводит только имена врачей, для которых не нашлось перевода в базе
    // (data-translate-name остался в разметке только в этом случае — см.
    // doctorCell в _renderTable). Комментарии сюда намеренно не входят.
    async _autoTranslateDoctorNames() {
        const lang = getLang();
        if (lang === 'ru') return;

        const nameNodes = [
            ...document.querySelectorAll('#active-appointments [data-translate-name]'),
            ...document.querySelectorAll('#history-appointments [data-translate-name]'),
        ];
        await Promise.all(nameNodes.map(async node => {
            const original = node.dataset.translateName;
            if (!original) return;
            const translated = await translateText(original, lang, 'name');
            if (translated && translated !== original) node.textContent = translated;
        }));

        this._translateFavoriteDoctor(lang);
    }

    _renderTable(tbodyId, appointments, emptyMessage, withActions) {
        const tbody = document.getElementById(tbodyId);
        if (!tbody) return;
        const cols = withActions ? 6 : 5;

        if (!appointments.length) {
            tbody.innerHTML = `<tr><td colspan="${cols}" class="panel-empty">${emptyMessage}</td></tr>`;
            return;
        }

        const lang = getLang();
        tbody.innerHTML = appointments.map(a => {
            const { label, cls } = this.statusMap[a.status] || { label: a.status, cls: '' };
            let actions = '';
            if (withActions) {
                if (a.status === 'pending') {
                    actions = `
                      <td class="col-actions">
                        <div class="panel-table-actions">
                            <button class="btn-tag btn-edit" data-action="reschedule" data-id="${a.id}" title="${escapeHtml(t('action_reschedule', 'Перенести'))}">🔄</button>
                            <button class="btn-tag btn-cancel" data-action="cancel" data-id="${a.id}" title="${escapeHtml(t('action_cancel', 'Отменить'))}">✕</button>
                        </div>
                      </td>`;
                } else {
                    // Подтверждённую запись пациент не может изменить сам — только по звонку в клинику
                    actions = `
                      <td class="col-actions">
                        <div class="panel-table-actions">
                            <button class="btn-tag btn-call-hint" data-action="call-hint" title="${escapeHtml(t('action_call_to_change', 'Позвоните администратору, чтобы изменить'))}">📞</button>
                        </div>
                      </td>`;
                }
            }
            // Сначала пробуем готовый перевод имени из базы (быстро и надёжно,
            // не зависит от внешнего переводческого API). Если для текущего
            // языка перевода в базе нет — оставляем span с data-атрибутом,
            // чтобы его перевёл внешний сервис (см. _autoTranslateDoctorNames).
            let doctorCell = '—';
            if (a.doctorName) {
                const dbName = lang !== 'ru' ? this._doctorTranslations.get(a.doctorId)?.[lang] : null;
                doctorCell = dbName
                    ? `<span>${escapeHtml(dbName)}</span>`
                    : `<span data-translate-name="${escapeHtml(a.doctorName)}">${escapeHtml(a.doctorName)}</span>`;
            }

            // Комментарий — свободный текст, для него нет перевода в базе,
            // поэтому автоматический перевод оказался ненадёжным (внешний
            // переводческий API не всегда успевает/отвечает). Вместо этого —
            // кнопка "🌐" рядом с комментарием, переводящая по клику
            // (см. _wireCommentTranslateButtons).
            const commentBtn = (lang !== 'ru' && a.comment)
                ? `<button type="button" class="row-translate-btn" data-row-translate="${a.id}" title="${escapeHtml(t('row_translate_btn', 'Перевести'))}">🌐</button>`
                : '';

            return `<tr data-appointment-row="${a.id}">
              <td>${doctorCell}</td>
              <td>${formatDate(a.appointmentDate)}</td>
              <td>${formatTime(a.appointmentDate)}</td>
              <td class="col-comment">${a.comment ? `<span data-translate-text="${escapeHtml(a.comment)}">${escapeHtml(a.comment)}</span> ${commentBtn}` : '—'}</td>
              <td><span class="status-badge ${cls}">${label}</span></td>
              ${actions}
            </tr>`;
        }).join('');

        if (withActions) this._attachActionHandlers(tbody);
        this._wireCommentTranslateButtons(tbodyId);
    }

    // Кнопка "🌐" переводит комментарий ТОЛЬКО этой строки по клику.
    // В отличие от имени врача, у комментария нет запасного перевода в базе
    // (это свободный текст пациента/администратора), поэтому автоматический
    // перевод при каждой смене языка оказался ненадёжным — часть запросов к
    // внешнему API просто не успевала. Кнопка даёт пациенту явный контроль:
    // нажал — подождал — увидел перевод, вместо тихого "не перевелось".
    _wireCommentTranslateButtons(tbodyId) {
        const tbody = document.getElementById(tbodyId);
        tbody?.querySelectorAll('.row-translate-btn').forEach(btn => {
            btn.addEventListener('click', async () => {
                const row = btn.closest('tr');
                const span = row?.querySelector('[data-translate-text]');
                if (!span) return;

                if (btn.dataset.state === 'translated') {
                    span.textContent = span.dataset.translateText;
                    btn.dataset.state = '';
                    btn.textContent = '🌐';
                    btn.title = t('row_translate_btn', 'Перевести');
                    return;
                }

                btn.disabled = true;
                btn.textContent = '⏳';
                const original = span.dataset.translateText;
                const translated = await translateText(original, getLang(), 'text');
                btn.disabled = false;

                if (translated && translated !== original) {
                    span.textContent = translated;
                    btn.dataset.state = 'translated';
                    btn.textContent = '↩';
                    btn.title = t('row_show_original_btn', 'Показать оригинал');
                } else {
                    btn.textContent = '⚠️';
                    setTimeout(() => { btn.textContent = '🌐'; }, 2500);
                }
            });
        });
    }

    // Автоматический перевод имени врача в карточке аналитики "Ваш лечащий
    // врач" на вкладке "История" — переводится сам при смене языка, без
    // отдельной кнопки. Если в разметке где-то ещё осталась кнопка
    // перевода — на всякий случай скрываем её, чтобы не висела без дела.
    async _translateFavoriteDoctor(lang) {
        const el = document.getElementById('stat-favorite-doctor');
        const btn = document.getElementById('stat-favorite-doctor-translate');
        if (btn) btn.classList.add('hidden');
        if (!el || !el.dataset.originalName) return;

        if (lang === 'ru') {
            el.textContent = el.dataset.originalName;
            return;
        }

        // Сначала — перевод из базы (мгновенно и надёжно), и только если
        // для этого языка его нет в базе — внешний переводческий API.
        const doctorId = el.dataset.doctorId ? Number(el.dataset.doctorId) : null;
        const dbName = doctorId != null ? this._doctorTranslations.get(doctorId)?.[lang] : null;
        if (dbName) {
            el.textContent = dbName;
            return;
        }

        const translated = await translateText(el.dataset.originalName, lang, 'name');
        if (translated) el.textContent = translated;
    }

    _attachActionHandlers(tbody) {
        tbody.querySelectorAll('[data-action="cancel"]').forEach(btn => {
            btn.addEventListener('click', () => this._cancelAppointment(btn.dataset.id));
        });
        tbody.querySelectorAll('[data-action="reschedule"]').forEach(btn => {
            btn.addEventListener('click', () => this._openReschedule(btn.dataset.id));
        });
        tbody.querySelectorAll('[data-action="call-hint"]').forEach(btn => {
            btn.addEventListener('click', () => {
                showError(t(
                    'patient_confirmed_call_required',
                    'Подтверждённую запись можно изменить только по телефону — позвоните администратору клиники: +7 (499) 999-99-99'
                ));
            });
        });
    }

    async _cancelAppointment(id) {
        const ok = await showConfirm(t('patient_cancel_confirm_text', 'Вы уверены, что хотите отменить эту запись? Это действие нельзя будет отменить.'), {
            title: t('patient_cancel_confirm_title', 'Отменить запись?'),
            confirmText: t('patient_cancel_confirm_yes', 'Да, отменить запись'),
            cancelText: t('patient_cancel_confirm_no', 'Не отменять'),
            danger: true,
            icon: '🗓️'
        });
        if (!ok) return;
        try {
            await apiFetch(`/appointmentrequest/${id}/cancel`, { method: 'PUT' });
            showSuccess(t('patient_cancel_success', 'Запись отменена'));
            this.loadAppointments();
        } catch (err) {
            showError(err.message || t('patient_cancel_error', 'Не удалось отменить запись'));
        }
    }

    _openReschedule(id) {
        const appt = this._data.find(a => String(a.id) === String(id));
        if (!this.rescheduleModal.wrap) return;
        this.rescheduleModal.id.value = id;
        this.rescheduleModal.datetime.value = toInputDateTime(appt?.appointmentDate) || '';
        this.rescheduleModal.wrap.classList.remove('hidden');
    }

    _hideReschedule() {
        this.rescheduleModal.wrap?.classList.add('hidden');
    }

    _setupRescheduleModal() {
        this.rescheduleModal.close?.addEventListener('click', () => this._hideReschedule());
        this.rescheduleModal.cancel?.addEventListener('click', () => this._hideReschedule());
        this.rescheduleModal.form?.addEventListener('submit', async e => {
            e.preventDefault();
            const id = this.rescheduleModal.id.value;
            const raw = this.rescheduleModal.datetime.value;
            if (!raw) { showError(t('patient_pick_datetime', 'Укажите новую дату и время')); return; }

            try {
                await apiFetch(`/appointmentrequest/${id}/reschedule`, {
                    method: 'PUT',
                    body: JSON.stringify({ appointmentDate: raw.length === 16 ? `${raw}:00` : raw })
                });
                showSuccess(t('patient_reschedule_sent', 'Запрос на перенос отправлен — ожидайте подтверждения администратора'));
                this._hideReschedule();
                this.loadAppointments();
            } catch (err) {
                showError(err.message || t('patient_reschedule_error', 'Не удалось перенести запись'));
            }
        });
    }

    // «История в цифрах»: считаем прямо из уже загруженных записей,
    // без дополнительных запросов к серверу.
    _renderHistoryStats(all, history) {
        const totalVisitsEl = document.getElementById('stat-total-visits');
        const clientSinceEl = document.getElementById('stat-client-since');
        const favoriteDoctorEl = document.getElementById('stat-favorite-doctor');

        const completed = history.filter(a => a.status === 'completed');
        if (totalVisitsEl) totalVisitsEl.textContent = completed.length;

        if (clientSinceEl) {
            const dates = all.map(a => a.createdAt).filter(Boolean).map(d => new Date(d)).filter(d => !Number.isNaN(d.getTime()));
            clientSinceEl.textContent = dates.length ? new Date(Math.min(...dates)).getFullYear() : '—';
        }

        if (favoriteDoctorEl) {
            const counts = {};
            completed.forEach(a => {
                if (!a.doctorName) return;
                const key = a.doctorId ?? a.doctorName;
                if (!counts[key]) counts[key] = { name: a.doctorName, doctorId: a.doctorId, count: 0 };
                counts[key].count++;
            });
            const top = Object.values(counts).sort((x, y) => y.count - x.count)[0];
            const name = top ? top.name : '—';
            favoriteDoctorEl.textContent = name;
            if (top) {
                favoriteDoctorEl.dataset.originalName = name;
                if (top.doctorId != null) favoriteDoctorEl.dataset.doctorId = top.doctorId;
                else delete favoriteDoctorEl.dataset.doctorId;
            } else {
                delete favoriteDoctorEl.dataset.originalName;
                delete favoriteDoctorEl.dataset.doctorId;
            }
            // Перевод этой карточки запускается из _autoTranslateDoctorNames()
            // сразу после отрисовки таблиц (использует резерв из базы, если
            // есть, иначе — внешний API, как и обычное имя врача в таблице).
        }
    }

    async loadProfile() {
        const fn = document.getElementById('profile-firstname');
        const em = document.getElementById('profile-email');
        const ph = document.getElementById('profile-phone');
        if (!fn && !em && !ph) return; // секции профиля нет в DOM

        try {
            const profile = await apiFetch('/auth/profile');
            if (fn) fn.value = profile.firstName || '';
            if (em) em.value = profile.email || '';
            if (ph) ph.value = profile.phone || '';

            paintAvatarEverywhere(profile.avatarUrl);
            initAvatarUploader({
                rootId: 'profile-avatar-uploader',
                initialUrl: profile.avatarUrl,
                fallbackIcon: '👤'
            });
        } catch (err) {
            console.error('loadProfile error:', err);
            showError(t('patient_profile_load_error', 'Не удалось загрузить данные профиля'));
        }
    }

    _setupProfileForms() {
        this.profileForm?.addEventListener('submit', async e => {
            e.preventDefault();
            const firstName = document.getElementById('profile-firstname')?.value.trim();
            const phone = document.getElementById('profile-phone')?.value.trim();

            if (!firstName) { showError(t('patient_enter_name', 'Укажите имя')); return; }

            try {
                const res = await apiFetch('/auth/profile', {
                    method: 'PUT',
                    body: JSON.stringify({ firstName, phone })
                });
                showSuccess(t('patient_profile_updated', 'Профиль обновлён'));

                if (res.firstName) {
                    sessionStorage.setItem('patientName', res.firstName);
                    const nameEl = document.getElementById('patient-name');
                    if (nameEl) nameEl.textContent = res.firstName;
                }
            } catch (err) {
                showError(err.message || t('patient_profile_update_error', 'Не удалось обновить профиль'));
            }
        });

        this.passwordForm?.addEventListener('submit', async e => {
            e.preventDefault();
            const current = document.getElementById('profile-current-password')?.value;
            const next = document.getElementById('profile-new-password')?.value;
            const repeat = document.getElementById('profile-new-password-repeat')?.value;

            if (next !== repeat) { showError(t('patient_passwords_mismatch', 'Новые пароли не совпадают')); return; }
            if (!next || next.length < 6) { showError(t('patient_password_too_short', 'Новый пароль должен содержать не менее 6 символов')); return; }

            try {
                const res = await apiFetch('/auth/change-password', {
                    method: 'PUT',
                    body: JSON.stringify({ currentPassword: current, newPassword: next })
                });
                showSuccess(t('patient_password_updated', 'Пароль изменён'));
                this.passwordForm.reset();
            } catch (err) {
                showError(err.message || t('patient_password_update_error', 'Не удалось изменить пароль'));
            }
        });
    }
}

document.addEventListener('DOMContentLoaded', () => new CabinetManager().init());