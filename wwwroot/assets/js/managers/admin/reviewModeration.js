import { runWhenDomReady } from '../../core/domReady.js';
import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError, renderPagination } from '../../services/ui.js';
import { formatDate } from '../../services/dateUtils.js';

const PAGE_SIZE = 15;

class ReviewModerationManager {
    constructor() {
        this.tbody = {
            pending: document.getElementById('admin-reviews-pending-body'),
            approved: document.getElementById('admin-reviews-approved-body'),
            rejected: document.getElementById('admin-reviews-rejected-body'),
        };
        this.paginationEl = {
            pending: document.getElementById('admin-reviews-pending-pagination'),
            approved: document.getElementById('admin-reviews-approved-pagination'),
            rejected: document.getElementById('admin-reviews-rejected-pagination'),
        };
        this._data = { pending: [], approved: [], rejected: [] };
        this._page = { pending: 1, approved: 1, rejected: 1 };
        this._total = { pending: 0, approved: 0, rejected: 0 };
        this._loadedTabs = new Set();
        this._loadPromises = new Map();

        this.modal = {
            wrap: document.getElementById('reject-review-modal'),
            form: document.getElementById('reject-review-form'),
            id: document.getElementById('reject-review-id'),
            reason: document.getElementById('reject-reason'),
            close: document.getElementById('reject-modal-close'),
            cancel: document.getElementById('reject-modal-cancel'),
        };
    }

    init() {
        if (!this.tbody.pending) return;

        this.modal.form?.addEventListener('submit', e => this._submitReject(e));
        this.modal.close?.addEventListener('click', () => this._hideModal());
        this.modal.cancel?.addEventListener('click', () => this._hideModal());

        const nav = document.querySelector('.panel-nav-link[data-section="reviews"]');
        nav?.addEventListener('click', () => this.loadTabOnce('pending'));

        document.querySelectorAll('#section-reviews .panel-tab[data-tab]').forEach(button => {
            button.addEventListener('click', () => {
                const key = String(button.dataset.tab || '').replace('reviews-', '');
                if (['pending', 'approved', 'rejected'].includes(key))
                    this.loadTabOnce(key);
            });
        });

        if (sessionStorage.getItem('admin_active_section') === 'reviews')
            this.loadTabOnce('pending');
    }

    loadTabOnce(key) {
        if (this._loadedTabs.has(key)) return Promise.resolve();
        if (this._loadPromises.has(key)) return this._loadPromises.get(key);

        const task = this._loadTab(key, this._page[key])
            .then(success => {
                if (success) this._loadedTabs.add(key);
                return success;
            })
            .finally(() => { this._loadPromises.delete(key); });
        this._loadPromises.set(key, task);
        return task;
    }

    async loadAll({ reset = false } = {}) {
        if (reset) {
            this._page = { pending: 1, approved: 1, rejected: 1 };
            this._loadedTabs.clear();
        }

        // Moderation changes can affect more than one tab, so explicit refreshes
        // update every tab sequentially instead of opening three SQL connections at once.
        for (const key of ['pending', 'approved', 'rejected']) {
            const success = await this._loadTab(key, this._page[key]);
            if (success) this._loadedTabs.add(key);
            else this._loadedTabs.delete(key);
        }
    }

    async _loadTab(key, page = 1) {
        const tbody = this.tbody[key];
        if (!tbody) return;

        tbody.innerHTML = `<tr><td colspan="6">Загрузка...</td></tr>`;

        try {
            const result = await apiFetch(`/review/admin/list/${encodeURIComponent(key)}?page=${page}&pageSize=${PAGE_SIZE}`);
            this._data[key] = Array.isArray(result?.items) ? result.items : [];
            this._page[key] = Number(result?.page) || 1;
            this._total[key] = Number(result?.total) || 0;
            this._render(key);
            return true;
        } catch (err) {
            console.error(`ReviewModeration [${key}] error:`, err);
            tbody.innerHTML = `<tr><td colspan="6" class="panel-error">
                ${this._esc(err?.message || 'Ошибка загрузки')}
                <button type="button" class="panel-btn-secondary" data-retry-review="${key}" style="margin-left:10px">Повторить</button>
            </td></tr>`;
            tbody.querySelector(`[data-retry-review="${key}"]`)?.addEventListener('click', () => this._loadTab(key, this._page[key]));
            if (this.paginationEl[key]) this.paginationEl[key].innerHTML = '';
            return false;
        }
    }

    _render(key) {
        const tbody = this.tbody[key];
        if (!tbody) return;

        const data = this._data[key] || [];
        const total = this._total[key] || 0;

        if (!data.length) {
            tbody.innerHTML = `<tr><td colspan="6" class="panel-empty">Отзывов нет</td></tr>`;
            if (this.paginationEl[key]) this.paginationEl[key].innerHTML = '';
            return;
        }

        const buildStars = (rating) => {
            let filled = '';
            let empty = '';
            for (let i = 0; i < rating; i++) filled += `<span style="animation-delay:${i * 70}ms">★</span>`;
            for (let i = 0; i < 5 - rating; i++) empty += `<span style="animation-delay:${(rating + i) * 70}ms">☆</span>`;
            return `<span class="review-stars">${filled}</span><span class="review-stars review-stars-empty">${empty}</span>`;
        };

        tbody.innerHTML = data.map(r => {
            const stars = buildStars(r.rating);

            if (key === 'pending') {
                return `<tr>
                    <td>#${r.id}</td>
                    <td>${this._esc(r.patientName)}<br><small>${this._esc(r.patientEmail)}</small></td>
                    <td>${stars}</td>
                    <td class="col-comment">${this._esc(r.text)}</td>
                    <td>${formatDate(r.createdAt)}</td>
                    <td class="col-actions">
                        <button class="btn-tag btn-confirm btn-approve-review" data-id="${r.id}" title="Одобрить">✓</button>
                        <button class="btn-tag btn-cancel btn-reject-review" data-id="${r.id}" title="Отклонить">✕</button>
                    </td>
                </tr>`;
            }

            if (key === 'approved') {
                return `<tr>
                    <td>#${r.id}</td>
                    <td>${this._esc(r.patientName)}</td>
                    <td>${stars}</td>
                    <td class="col-comment">${this._esc(r.text)}</td>
                    <td>${formatDate(r.moderatedAt)}</td>
                </tr>`;
            }

            return `<tr>
                <td>#${r.id}</td>
                <td>${this._esc(r.patientName)}</td>
                <td>${stars}</td>
                <td class="col-comment">${this._esc(r.text)}</td>
                <td>${this._esc(r.rejectionReason)}</td>
            </tr>`;
        }).join('');

        if (key === 'pending') this._attachRowHandlers();

        renderPagination(this.paginationEl[key], {
            page: this._page[key],
            totalItems: total,
            pageSize: PAGE_SIZE,
            onPageChange: (p) => this._loadTab(key, p)
        });
    }

    _attachRowHandlers() {
        this.tbody.pending.querySelectorAll('.btn-approve-review').forEach(btn => {
            btn.addEventListener('click', () => this._approve(Number(btn.dataset.id)));
        });
        this.tbody.pending.querySelectorAll('.btn-reject-review').forEach(btn => {
            btn.addEventListener('click', () => this._openRejectModal(Number(btn.dataset.id)));
        });
    }

    async _approve(id) {
        if (!confirm('Опубликовать этот отзыв на сайте?')) return;

        try {
            await apiFetch(`/review/admin/${id}/moderate`, {
                method: 'PUT',
                body: JSON.stringify({ status: 'approved' })
            });
            showSuccess('Отзыв одобрен и опубликован');
            await this.loadAll();
        } catch (err) {
            showError(err.message || 'Не удалось одобрить отзыв');
        }
    }

    _openRejectModal(id) {
        if (this.modal.id) this.modal.id.value = id;
        if (this.modal.reason) this.modal.reason.value = '';
        this.modal.wrap?.classList.remove('hidden');
    }

    _hideModal() {
        this.modal.wrap?.classList.add('hidden');
    }

    async _submitReject(e) {
        e.preventDefault();
        const id = Number(this.modal.id?.value);
        const reason = this.modal.reason?.value.trim();

        if (!reason) {
            showError('Укажите причину отклонения отзыва');
            return;
        }

        try {
            await apiFetch(`/review/admin/${id}/moderate`, {
                method: 'PUT',
                body: JSON.stringify({ status: 'rejected', rejectionReason: reason })
            });
            this._hideModal();
            showSuccess('Отзыв отклонён, пациент увидит причину в личном кабинете');
            await this.loadAll();
        } catch (err) {
            showError(err.message || 'Не удалось отклонить отзыв');
        }
    }

    _esc(str) {
        const div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }
}

runWhenDomReady(() => {
    const manager = new ReviewModerationManager();
    manager.init();
    window.ReviewModerationManagerInstance = manager;
});

export { ReviewModerationManager };
