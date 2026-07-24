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

        this.loadAll();
    }

    async loadAll() {
        await Promise.all([
            this._loadTab('pending', '/review/admin/pending'),
            this._loadTab('approved', '/review/admin/approved'),
            this._loadTab('rejected', '/review/admin/rejected'),
        ]);
    }

    async _loadTab(key, endpoint) {
        const tbody = this.tbody[key];
        if (!tbody) return;

        try {
            const data = await apiFetch(endpoint);
            this._data[key] = data;
            this._page[key] = 1;
            this._render(key);
        } catch (err) {
            console.error(`ReviewModeration [${key}] error:`, err);
            tbody.innerHTML = `<tr><td colspan="6" class="panel-error">Ошибка загрузки</td></tr>`;
        }
    }

    _render(key) {
        const tbody = this.tbody[key];
        if (!tbody) return;

        const all = this._data[key] || [];

        if (!all.length) {
            tbody.innerHTML = `<tr><td colspan="6" class="panel-empty">Отзывов нет</td></tr>`;
            if (this.paginationEl[key]) this.paginationEl[key].innerHTML = '';
            return;
        }

        const totalPages = Math.max(1, Math.ceil(all.length / PAGE_SIZE));
        if (this._page[key] > totalPages) this._page[key] = totalPages;
        const start = (this._page[key] - 1) * PAGE_SIZE;
        const data = all.slice(start, start + PAGE_SIZE);

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
            totalItems: all.length,
            pageSize: PAGE_SIZE,
            onPageChange: (p) => { this._page[key] = p; this._render(key); }
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
            this.loadAll();
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
            this.loadAll();
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

document.addEventListener('DOMContentLoaded', () => {
    const manager = new ReviewModerationManager();
    manager.init();
    window.ReviewModerationManagerInstance = manager;
});

export { ReviewModerationManager };