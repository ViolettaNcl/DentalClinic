import { apiFetch } from '../services/apiClient.js';
import { escapeHtml, showConfirm } from '../services/ui.js';
import { realtime } from '../services/realtime.js';
import { t, getLang, onLanguageChange } from './i18n.js';

/**
 * Колокольчик уведомлений в шапке сайта — виден только вошедшим пациентам
 * (не гостям и не админу, у которого своя панель).
 * Показывает непрочитанные уведомления о смене статуса записи и модерации отзыва.
 */
class NotificationBell {
    constructor() {
        this.wrap = document.getElementById('notification-bell');
        this.btn = document.getElementById('notification-bell-btn');
        this.badge = document.getElementById('notification-badge');
        this.dropdown = document.getElementById('notification-dropdown');
        this.list = document.getElementById('notification-list');
        this.markAllBtn = document.getElementById('notification-mark-all');

        this.pollInterval = null;
        this._lastItems = null;

        this.icons = {
            welcome: '👋',
            appointment_confirmed: '✅',
            appointment_cancelled: '❌',
            appointment_completed: '🦷',
            appointment_reminder: '⏰',
            review_approved: '⭐',
            review_rejected: '📝'
        };
    }

    init() {
        if (!this.wrap || !this.btn) return;

        const patientId = sessionStorage.getItem('patientId');
        const role = sessionStorage.getItem('userRole');

        // Колокольчик только для вошедшего пациента (не гостя, не админа)
        if (!patientId || role?.toLowerCase() !== 'patient') return;

        // Плавное появление: сначала переводим в поток (display), затем на
        // следующем кадре добавляем класс — иначе браузер не анимирует переход
        // из-за отсутствия промежуточного рендера между display:none и flex.
        this.wrap.style.display = 'flex';
        requestAnimationFrame(() => this.wrap.classList.add('notification-bell--visible'));
        this.btn.setAttribute('aria-label', t('notif_aria_label', 'Уведомления'));

        this.btn.addEventListener('click', e => {
            e.stopPropagation();
            const isOpen = this.wrap.classList.toggle('open');
            if (isOpen) this.loadList();
        });

        document.addEventListener('click', e => {
            if (!this.wrap.contains(e.target)) this.wrap.classList.remove('open');
        });

        this.markAllBtn?.addEventListener('click', () => this.markAllRead());
        this._ensureDeleteAllButton();

        this.loadUnreadCount();

        // Realtime: как только приходит уведомление — сразу обновляем счётчик и,
        // если список открыт, вставляем его первым, без ожидания следующего опроса.
        realtime.on('ReceiveNotification', (n) => this._onRealtimeNotification(n));
        realtime.connect();

        // Опрос оставляем как редкий fallback — на случай если realtime-соединение
        // не установилось (например, сеть заблокировала WebSocket) или разорвалось
        // без переподключения. Раз в 2 минуты этого достаточно, если push работает.
        this.pollInterval = setInterval(() => this.loadUnreadCount(), 120000);

        // При смене языка перерисовываем всю "хром"-часть виджета: заголовок,
        // кнопки и (если список уже был загружен) сам список уведомлений.
        onLanguageChange(() => {
            this.btn.setAttribute('aria-label', t('notif_aria_label', 'Уведомления'));
            this._ensureDeleteAllButton(true);
            if (this._lastItems) this._renderList(this._lastItems);
        });
    }

    _ensureDeleteAllButton(force = false) {
        if (!this.markAllBtn) return;
        if (document.getElementById('notification-delete-all') && !force) return;

        // Компактная кнопка с иконкой и коротким текстом — не крошечный кружок,
        // но и не занимает половину шапки, как было раньше.
        this.markAllBtn.classList.add('notification-pill-btn');
        this.markAllBtn.innerHTML = `<span class="notification-pill-icon">✓</span> ${escapeHtml(t('notif_mark_all_read', 'Прочитано'))}`;

        const wrap = this.markAllBtn.parentElement;
        let toolbar = wrap?.querySelector('.notification-toolbar');
        if (!toolbar) {
            toolbar = document.createElement('div');
            toolbar.className = 'notification-toolbar';
            this.markAllBtn.insertAdjacentElement('beforebegin', toolbar);
            toolbar.appendChild(this.markAllBtn);
        }

        let btn = document.getElementById('notification-delete-all');
        if (!btn) {
            btn = document.createElement('button');
            btn.type = 'button';
            btn.id = 'notification-delete-all';
            btn.className = 'notification-pill-btn notification-pill-btn--danger';
            btn.addEventListener('click', () => this.deleteAll());
            toolbar.appendChild(btn);
        }
        btn.innerHTML = `<span class="notification-pill-icon">🗑</span> ${escapeHtml(t('notif_clear_all', 'Очистить всё'))}`;
    }

    _onRealtimeNotification(n) {
        // Обновляем бейдж не запросом на сервер, а локально — на 1 больше текущего
        const current = this.badge?.textContent === '9+' ? 10 : Number(this.badge?.textContent || 0);
        this._renderBadge((this.badge?.style.display === 'flex' ? current : 0) + 1);
        this._playArriveAnimation();

        if (this.wrap.classList.contains('open') && this.list) {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'notification-item unread';
            item.dataset.id = n.id;
            item.innerHTML = `
                <span class="notification-icon">${this.icons[n.type] || '🔔'}</span>
                <span class="notification-text">
                    <span class="notification-message">${escapeHtml(n.message)}</span>
                    <span class="notification-time">${this._formatTime(n.createdAt)}</span>
                </span>
                <span class="notification-delete" title="${escapeHtml(t('notif_delete', 'Удалить'))}">🗑️</span>`;
            item.addEventListener('click', (e) => {
                if (e.target.closest('.notification-delete')) return;
                this.markRead(n.id, item);
            });
            item.querySelector('.notification-delete').addEventListener('click', (e) => {
                e.stopPropagation();
                this.deleteOne(n.id, item);
            });
            this.list.prepend(item);
        }
        if (this._lastItems) this._lastItems.unshift(n);
    }

    async loadUnreadCount() {
        try {
            const res = await apiFetch('/notification/unread-count');
            this._renderBadge(res.count || 0);
        } catch (err) {
            console.error('notification unread-count error:', err);
        }
    }

    _renderBadge(count) {
        if (!this.badge) return;
        if (count > 0) {
            this.badge.textContent = count > 9 ? '9+' : String(count);
            this.badge.style.display = 'flex';
            this.wrap?.classList.add('has-unread');
        } else {
            this.badge.style.display = 'none';
            this.wrap?.classList.remove('has-unread');
        }
    }

    // Колокольчик "выезжает" справа и пружиня возвращается на место — сигнал
    // о новом уведомлении. Класс снимается и ставится заново, чтобы анимация
    // перезапускалась даже если несколько уведомлений приходят подряд.
    _playArriveAnimation() {
        if (!this.btn) return;
        this.btn.classList.remove('notification-bell-btn--new');
        void this.btn.offsetWidth;
        this.btn.classList.add('notification-bell-btn--new');
        clearTimeout(this._arriveTimeout);
        this._arriveTimeout = setTimeout(() => this.btn.classList.remove('notification-bell-btn--new'), 700);
    }

    async loadList() {
        if (!this.list) return;
        this.list.innerHTML = `<div class="notification-empty">${escapeHtml(t('ui_loading', 'Загрузка...'))}</div>`;

        try {
            const items = await apiFetch('/notification');
            this._lastItems = items || [];
            this._renderList(this._lastItems);
        } catch (err) {
            this.list.innerHTML = `<div class="notification-empty">${escapeHtml(t('notif_load_error', 'Не удалось загрузить'))}</div>`;
        }
    }

    _renderList(items) {
        if (!items.length) {
            this.list.innerHTML = `<div class="notification-empty">${escapeHtml(t('notif_empty', 'Пока нет уведомлений'))}</div>`;
            return;
        }

        this.list.innerHTML = items.map(n => `
            <button type="button" class="notification-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}">
                <span class="notification-icon">${this.icons[n.type] || '🔔'}</span>
                <span class="notification-text">
                    <span class="notification-message">${escapeHtml(n.message)}</span>
                    <span class="notification-time">${this._formatTime(n.createdAt)}</span>
                </span>
                <span class="notification-delete" title="${escapeHtml(t('notif_delete', 'Удалить'))}">🗑️</span>
            </button>
        `).join('');

        this.list.querySelectorAll('.notification-item').forEach(el => {
            el.addEventListener('click', (e) => {
                if (e.target.closest('.notification-delete')) return;
                this.markRead(Number(el.dataset.id), el);
            });
            el.querySelector('.notification-delete')?.addEventListener('click', (e) => {
                e.stopPropagation();
                this.deleteOne(Number(el.dataset.id), el);
            });
        });
    }

    _formatTime(iso) {
        try {
            const d = new Date(iso);
            const locales = { ru: 'ru-RU', en: 'en-US', fr: 'fr-FR', el: 'el-GR', ar: 'ar-EG' };
            return d.toLocaleString(locales[getLang()] || 'ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
        } catch {
            return '';
        }
    }

    async markRead(id, el) {
        el?.classList.remove('unread');
        try {
            await apiFetch(`/notification/${id}/read`, { method: 'PUT' });
            this.loadUnreadCount();
        } catch (err) {
            console.error('mark-read error:', err);
        }
    }

    async markAllRead() {
        try {
            await apiFetch('/notification/read-all', { method: 'PUT' });
            this.list?.querySelectorAll('.notification-item.unread').forEach(el => el.classList.remove('unread'));
            this._renderBadge(0);
        } catch (err) {
            console.error('mark-all-read error:', err);
        }
    }

    // Удаление одного уведомления — обязательно с подтверждением, чтобы случайный
    // клик по корзине рядом с текстом не стирал важное уведомление безвозвратно
    async deleteOne(id, el) {
        const ok = await showConfirm(t('notif_delete_one_confirm', 'Удалить это уведомление? Это действие нельзя отменить.'), { danger: true, icon: '🗑️' });
        if (!ok) return;

        try {
            await apiFetch(`/notification/${id}`, { method: 'DELETE' });
            const wasUnread = el?.classList.contains('unread');
            el?.remove();
            if (this._lastItems) this._lastItems = this._lastItems.filter(x => x.id !== id);
            if (wasUnread) this.loadUnreadCount();
            if (this.list && !this.list.querySelector('.notification-item')) {
                this.list.innerHTML = `<div class="notification-empty">${escapeHtml(t('notif_empty', 'Пока нет уведомлений'))}</div>`;
            }
        } catch (err) {
            console.error('delete notification error:', err);
        }
    }

    async deleteAll() {
        const ok = await showConfirm(t('notif_delete_all_confirm', 'Удалить ВСЕ уведомления? Это действие нельзя отменить.'), { danger: true, icon: '🗑️' });
        if (!ok) return;

        try {
            await apiFetch('/notification', { method: 'DELETE' });
            this._lastItems = [];
            if (this.list) this.list.innerHTML = `<div class="notification-empty">${escapeHtml(t('notif_empty', 'Пока нет уведомлений'))}</div>`;
            this._renderBadge(0);
        } catch (err) {
            console.error('delete-all notifications error:', err);
        }
    }
}

export { NotificationBell };