import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError } from '../../services/ui.js';
import { formatDate } from '../../services/dateUtils.js';
import { t, onLanguageChange, getLang } from '../../core/i18n.js';
import { translateReviewText } from '../../services/reviewTranslate.js';

/**
 * Управляет разделом "Мои отзывы" в личном кабинете пациента:
 * список своих отзывов со статусами + баннер-уведомление,
 * если отзыв был отклонён модератором, + форма нового отзыва.
 */
class MyReviewsManager {
    constructor() {
        this.patientId = sessionStorage.getItem('patientId');
        this.listEl = document.getElementById('my-reviews-list');
        this.noticeEl = document.getElementById('review-rejection-notice');

        this.reviewModal = document.getElementById('review-modal');
        this.reviewForm = document.getElementById('review-form');
        this.ratingPicker = document.getElementById('review-rating-picker');
        this.ratingHint = document.getElementById('review-rating-hint');
        this.reviewTextarea = document.getElementById('review-text');
        this.charCount = document.getElementById('review-char-count');

        this.selectedRating = 0;
        this._lastReviews = null;
        this._buildDicts();

        // При смене языка пересобираем подписи статусов/оценок и перерисовываем
        // уже загруженный список, чтобы он не остался на предыдущем языке.
        onLanguageChange(() => {
            this._buildDicts();
            if (this._lastReviews) {
                this._renderNotice(this._lastReviews);
                this._renderList(this._lastReviews);
            }
            if (this.ratingHint) {
                this.ratingHint.textContent = this.selectedRating ? this.ratingHints[this.selectedRating] : t('rating_hint_default', 'Выберите оценку');
            }
        });
    }

    _buildDicts() {
        this.statusMap = {
            pending: { label: t('myreview_status_pending', 'На проверке'), cls: 'status-pending' },
            approved: { label: t('myreview_status_approved', 'Опубликован'), cls: 'status-confirmed' },
            rejected: { label: t('myreview_status_rejected', 'Отклонён'), cls: 'status-cancelled' },
        };
        this.ratingHints = {
            1: t('rating_hint_1', 'Плохо 😞'),
            2: t('rating_hint_2', 'Не очень 😐'),
            3: t('rating_hint_3', 'Нормально 🙂'),
            4: t('rating_hint_4', 'Хорошо 😊'),
            5: t('rating_hint_5', 'Отлично! 🤩')
        };
    }

    init() {
        if (!this.listEl || !this.patientId) return;
        this._setupModal();
        this._setupLeaveButton();
        this.load();
    }

    async load() {
        try {
            const reviews = await apiFetch(`/review/patient/${this.patientId}`);
            this._lastReviews = reviews;
            this._renderNotice(reviews);
            this._renderList(reviews);
        } catch (err) {
            console.error('MyReviewsManager load error:', err);
            this.listEl.innerHTML = `<p class="panel-error">${t('myreview_load_error', 'Не удалось загрузить ваши отзывы')}</p>`;
        }
    }

    _renderNotice(reviews) {
        const unread = reviews.find(r => r.status === 'rejected' && !r.isNotificationRead);
        if (!unread) {
            this.noticeEl.innerHTML = '';
            return;
        }

        const reasonText = unread.rejectionReason || t('myreview_rejection_default', 'отзыв не прошёл проверку модератора.');

        this.noticeEl.innerHTML = `
            <div class="review-notice-banner" data-review-id="${unread.id}">
                <span class="notice-icon">⚠️</span>
                <div>
                    <strong>${t('myreview_notice_title', 'Ваш отзыв не был опубликован')}</strong>
                    ${t('myreview_notice_text', 'Вы нарушили правила публикации отзывов: {reason}').replace('{reason}', this._escape(reasonText))}
                </div>
                <button type="button" class="notice-dismiss">${t('myreview_notice_dismiss', 'Понятно, скрыть')}</button>
            </div>
        `;

        this.noticeEl.querySelector('.notice-dismiss')?.addEventListener('click', async () => {
            try {
                await apiFetch(`/review/${unread.id}/mark-read`, { method: 'POST' });
            } catch (err) {
                console.error('mark-read error:', err);
            }
            this.noticeEl.innerHTML = '';
        });
    }

    _renderList(reviews) {
        if (!reviews.length) {
            this.listEl.innerHTML = `<p class="panel-empty">${t('myreview_none_yet', 'Вы ещё не оставляли отзывов')}</p>`;
            return;
        }

        this.listEl.innerHTML = reviews.map(r => {
            const { label, cls } = this.statusMap[r.status] || { label: r.status, cls: '' };
            const stars = Array.from({ length: 5 }, (_, i) =>
                `<span class="star ${i < r.rating ? 'filled' : ''}">★</span>`
            ).join('');

            const rejection = r.status === 'rejected'
                ? `<div class="my-review-rejection">${t('myreview_rejection_reason_label', 'Причина отклонения: {reason}').replace('{reason}', this._escape(r.rejectionReason || '—'))}</div>`
                : '';

            const translateBtn = getLang() !== 'ru'
                ? `<button type="button" class="review-translate-btn" data-review-id="${r.id}">🌐 ${t('review_translate_btn', 'Перевести')}</button>`
                : '';

            return `
                <div class="my-review-card">
                    <div class="my-review-card-top">
                        <span class="star-rating">${stars}</span>
                        <span class="status-badge ${cls}">${label}</span>
                        <span class="review-date">${formatDate(r.createdAt)}</span>
                    </div>
                    <p class="my-review-text"><span data-review-text-id="${r.id}" data-original-text="${this._escape(r.text)}">${this._escape(r.text)}</span></p>
                    ${translateBtn}
                    ${rejection}
                </div>
            `;
        }).join('');

        // Перевод текста своих отзывов — только по клику на кнопку "Перевести"
        // у карточки, не автоматически (бережём дневной лимит переводческого API).
        this._wireTranslateButtons(this.listEl);
    }

    // Обработчик кнопки "Перевести"/"Показать оригинал" на карточке своего отзыва —
    // логика идентична публичной карусели отзывов (см. reviewsManager.js).
    _wireTranslateButtons(container) {
        container?.querySelectorAll('.review-translate-btn').forEach(btn => {
            btn.addEventListener('click', async () => {
                const id = btn.dataset.reviewId;
                const textEl = container.querySelector(`[data-review-text-id="${id}"]`);
                if (!textEl) return;

                if (btn.dataset.state === 'translated') {
                    textEl.textContent = textEl.dataset.originalText;
                    btn.dataset.state = '';
                    btn.innerHTML = `🌐 ${t('review_translate_btn', 'Перевести')}`;
                    return;
                }

                const original = textEl.dataset.originalText;
                btn.disabled = true;
                btn.innerHTML = `⏳ ${t('review_translating', 'Переводим...')}`;

                const translated = await translateReviewText(id, original, getLang());

                btn.disabled = false;
                if (translated && translated !== original) {
                    textEl.textContent = translated;
                    btn.dataset.state = 'translated';
                    btn.innerHTML = `↩ ${t('review_show_original_btn', 'Показать оригинал')}`;
                } else {
                    btn.innerHTML = `⚠️ ${t('review_translate_failed', 'Не удалось перевести')}`;
                    setTimeout(() => {
                        btn.innerHTML = `🌐 ${t('review_translate_btn', 'Перевести')}`;
                    }, 2500);
                }
            });
        });
    }

    _escape(str) {
        const div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }

    _setupLeaveButton() {
        document.querySelectorAll('.btn-leave-review').forEach(btn => {
            btn.addEventListener('click', () => {
                this._resetForm();
                if (this.reviewModal) this.reviewModal.style.display = 'block';
            });
        });
    }

    _setupModal() {
        this.reviewModal?.querySelectorAll('.close').forEach(btn =>
            btn.addEventListener('click', () => this._closeModal())
        );
        this.reviewModal?.addEventListener('click', e => {
            if (e.target === this.reviewModal) this._closeModal();
        });

        this.ratingPicker?.querySelectorAll('.star').forEach(star => {
            star.addEventListener('click', () => {
                this.selectedRating = Number(star.dataset.value);
                this._paintRating(this.selectedRating);
            });
            star.addEventListener('mouseenter', () => this._paintRating(Number(star.dataset.value)));
        });
        this.ratingPicker?.addEventListener('mouseleave', () => this._paintRating(this.selectedRating));

        this.reviewTextarea?.addEventListener('input', () => {
            if (this.charCount) this.charCount.textContent = `${this.reviewTextarea.value.length}/1000`;
        });

        this.reviewForm?.addEventListener('submit', e => this._submit(e));
    }

    _paintRating(value) {
        this.ratingPicker?.querySelectorAll('.star').forEach(star => {
            star.classList.toggle('filled', Number(star.dataset.value) <= value);
        });
        if (this.ratingHint) this.ratingHint.textContent = value ? this.ratingHints[value] : t('rating_hint_default', 'Выберите оценку');
    }

    _resetForm() {
        this.reviewForm?.reset();
        this.selectedRating = 0;
        this._paintRating(0);
        if (this.charCount) this.charCount.textContent = '0/1000';
    }

    _closeModal() {
        if (this.reviewModal) this.reviewModal.style.display = 'none';
    }

    async _submit(e) {
        e.preventDefault();
        const text = this.reviewTextarea?.value.trim() || '';

        if (!this.selectedRating) {
            showError(t('review_pick_rating', 'Пожалуйста, выберите оценку от 1 до 5 звёзд'));
            return;
        }
        if (text.length < 10) {
            showError(t('review_text_too_short', 'Текст отзыва должен содержать не менее 10 символов'));
            return;
        }

        try {
            const res = await apiFetch('/review', {
                method: 'POST',
                body: JSON.stringify({ patientId: Number(this.patientId), rating: this.selectedRating, text })
            });
            this._closeModal();
            showSuccess(t('review_submitted', 'Отзыв отправлен на проверку модератору'));
            this.load();
        } catch (err) {
            showError(err.message || t('review_submit_error', 'Не удалось отправить отзыв'));
        }
    }
}

document.addEventListener('DOMContentLoaded', () => new MyReviewsManager().init());

export { MyReviewsManager };