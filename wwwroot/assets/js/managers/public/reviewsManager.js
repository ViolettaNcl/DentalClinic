import { apiFetch } from '../../services/apiClient.js';
import { showSuccess, showError } from '../../services/ui.js';
import { formatDate } from '../../services/dateUtils.js';
import { t, getLang, onLanguageChange } from '../../core/i18n.js';
import { translateReviewText } from '../../services/reviewTranslate.js';

/**
 * Управляет секцией "Отзывы наших пациентов" на главной странице:
 * загрузка одобренных отзывов + средний рейтинг, карусель с точками
 * и стрелками, сортировка, а также форма "Оставить отзыв"
 * (с проверкой авторизации и модалкой "нужен аккаунт" для гостей).
 */
class PublicReviewsManager {
    constructor() {
        this.section = document.getElementById('reviews-section');
        if (!this.section) return;

        this.avgScoreEl = document.getElementById('reviews-avg-score');
        this.avgStarsEl = document.getElementById('reviews-avg-stars');
        this.countEl = document.getElementById('reviews-count');
        this.statRatingEl = document.getElementById('stat-rating-value');

        this.sortEl = document.getElementById('reviews-sort');
        this.sortToggle = document.getElementById('reviews-sort-toggle');
        this.sortMenu = document.getElementById('reviews-sort-menu');
        this.sortLabel = document.getElementById('reviews-sort-label');

        this.trackWrap = this.section.querySelector('.reviews-track-wrap');
        this.trackEl = document.getElementById('reviews-track');
        this.dotsEl = document.getElementById('reviews-dots');
        this.prevBtn = document.getElementById('reviews-prev');
        this.nextBtn = document.getElementById('reviews-next');

        this.loginRequiredModal = document.getElementById('review-login-required');
        this.reviewModal = document.getElementById('review-modal');
        this.reviewForm = document.getElementById('review-form');
        this.ratingPicker = document.getElementById('review-rating-picker');
        this.ratingHint = document.getElementById('review-rating-hint');
        this.reviewTextarea = document.getElementById('review-text');
        this.charCount = document.getElementById('review-char-count');

        this.reviews = [];
        this.sortMode = 'newest';
        this.page = 0;
        this.selectedRating = 0;

        this._buildRatingHints();

        this._resizeHandler = () => this._slideTo(this.page, false);
    }

    _buildRatingHints() {
        this.ratingHints = {
            1: t('rating_hint_1', 'Плохо 😞'),
            2: t('rating_hint_2', 'Не очень 😐'),
            3: t('rating_hint_3', 'Нормально 🙂'),
            4: t('rating_hint_4', 'Хорошо 😊'),
            5: t('rating_hint_5', 'Отлично! 🤩')
        };
    }

    init() {
        if (!this.section) return;
        this._setupSort();
        this._setupCarouselNav();
        this._setupLeaveButton();
        this._setupLoginRequiredModal();
        this._setupReviewModal();
        window.addEventListener('resize', this._resizeHandler);
        onLanguageChange(() => {
            const activeBtn = this.sortMenu?.querySelector('.reviews-sort-option.active');
            if (this.sortLabel && activeBtn) this.sortLabel.textContent = t(activeBtn.dataset.labelKey, this.sortLabel.textContent);
            if (this.reviews.length) {
                this._renderSummary(this._lastAverage || 0, this._lastCount || 0);
                this._applySortAndRender();
            }
            // Пересобираем подсказки под звёздами ("Отлично!", "Нормально" и т.д.)
            // и, если рейтинг уже выбран, сразу обновляем видимую подпись.
            this._buildRatingHints();
            if (this.ratingHint) {
                this.ratingHint.textContent = this.selectedRating ? this.ratingHints[this.selectedRating] : t('rating_hint_default', 'Выберите оценку');
            }
        });
        this.load();
    }

    // =========================
    // Загрузка данных
    // =========================
    async load() {
        try {
            const data = await apiFetch('/review/approved');
            this.reviews = data.reviews || [];
            this._lastAverage = data.average || 0;
            this._lastCount = data.count || 0;
            this._renderSummary(this._lastAverage, this._lastCount);
            this._applySortAndRender();
        } catch (err) {
            console.error('PublicReviewsManager load error:', err);
            if (this.trackEl) {
                this.trackEl.innerHTML = `<div class="reviews-empty">${t('reviews_load_error', 'Не удалось загрузить отзывы. Попробуйте обновить страницу.')}</div>`;
            }
            if (this.countEl) this.countEl.textContent = t('reviews_unavailable', 'Отзывы недоступны');
        }
    }

    _renderSummary(average, count) {
        if (this.avgScoreEl) this.avgScoreEl.textContent = count > 0 ? average.toFixed(1) : '—';
        if (this.countEl) this.countEl.textContent = count > 0 ? this._pluralizeCount(count) : t('reviews_none_yet', 'Отзывов пока нет');

        if (this.avgStarsEl) {
            const rounded = Math.round(average);
            this.avgStarsEl.innerHTML = Array.from({ length: 5 }, (_, i) =>
                `<span class="star ${i < rounded ? 'filled' : ''}">★</span>`
            ).join('');
        }

        if (this.statRatingEl) {
            this.statRatingEl.textContent = count > 0 ? `${average.toFixed(1)}★` : '—★';
        }
    }

    _pluralizeCount(n) {
        if (getLang() !== 'ru') {
            return t('reviews_count_text', '{n} reviews').replace('{n}', n);
        }
        const mod10 = n % 10;
        const mod100 = n % 100;
        let word = 'отзывов';
        if (mod10 === 1 && mod100 !== 11) word = 'отзыв';
        else if ([2, 3, 4].includes(mod10) && ![12, 13, 14].includes(mod100)) word = 'отзыва';
        return `${n} ${word}`;
    }

    // =========================
    // Сортировка
    // =========================
    _setupSort() {
        this.sortToggle?.addEventListener('click', () => {
            this.sortEl?.classList.toggle('open');
        });

        document.addEventListener('click', e => {
            if (this.sortEl && !this.sortEl.contains(e.target)) {
                this.sortEl.classList.remove('open');
            }
        });

        this.sortMenu?.querySelectorAll('.reviews-sort-option').forEach(btn => {
            btn.addEventListener('click', () => {
                this.sortMode = btn.dataset.value;
                if (this.sortLabel) this.sortLabel.textContent = t(btn.dataset.labelKey, btn.textContent.trim());
                this.sortMenu.querySelectorAll('.reviews-sort-option').forEach(b => b.classList.toggle('active', b === btn));
                this.sortEl?.classList.remove('open');
                this.page = 0;
                this._applySortAndRender();
            });
        });
    }

    _applySortAndRender() {
        const sorted = [...this.reviews].sort((a, b) => {
            switch (this.sortMode) {
                case 'oldest':
                    return new Date(a.createdAt) - new Date(b.createdAt);
                case 'rating_desc':
                    return b.rating - a.rating || new Date(b.createdAt) - new Date(a.createdAt);
                case 'rating_asc':
                    return a.rating - b.rating || new Date(b.createdAt) - new Date(a.createdAt);
                case 'newest':
                default:
                    return new Date(b.createdAt) - new Date(a.createdAt);
            }
        });

        this._renderCarousel(sorted);
    }

    // =========================
    // Карусель
    // =========================
    _cardsPerView() {
        const w = window.innerWidth;
        if (w >= 1050) return 3;
        if (w >= 700) return 2;
        return 1;
    }

    _renderCarousel(reviews) {
        if (!this.trackEl) return;

        if (!reviews.length) {
            this.trackEl.innerHTML = `<div class="reviews-empty">${t('reviews_empty_carousel', 'Пока нет отзывов — станьте первым!')}</div>`;
            this.dotsEl.innerHTML = '';
            if (this.prevBtn) this.prevBtn.style.display = 'none';
            if (this.nextBtn) this.nextBtn.style.display = 'none';
            return;
        }

        this.trackEl.innerHTML = reviews.map(r => this._reviewCardHtml(r)).join('');

        this._totalPages = Math.max(1, Math.ceil(reviews.length / this._cardsPerView()));
        this.page = Math.min(this.page, this._totalPages - 1);

        this._renderDots();
        this._updateNavButtons();
        this._slideTo(this.page, false);

        // Автоматический перевод всех карточек отключён (экономим лимит API) —
        // вместо этого на каждой карточке есть кнопка "Перевести", переводим
        // только то, что реально попросил посмотреть пользователь.
        this._wireTranslateButtons(this.trackEl);
    }

    _reviewCardHtml(r) {
        const name = r.patientName || t('reviews_default_patient', 'Пациент');
        const initial = name.trim().charAt(0).toUpperCase() || '?';
        const stars = Array.from({ length: 5 }, (_, i) =>
            `<span class="star ${i < r.rating ? 'filled' : ''}">★</span>`
        ).join('');

        // Кнопка "Перевести" показывается только если выбранный язык интерфейса
        // не русский (отзывы пишут на русском по умолчанию — переводить самих
        // себя незачем). Перевод запускается только по клику, а не автоматически
        // для всех карточек сразу — это бережёт дневной лимит переводческого API.
        const translateBtn = getLang() !== 'ru'
            ? `<button type="button" class="review-translate-btn" data-review-id="${r.id}">🌐 ${t('review_translate_btn', 'Перевести')}</button>`
            : '';

        return `
            <div class="review-card">
                <div class="review-card-header">
                    <div class="review-avatar">${this._escape(initial)}</div>
                    <div class="review-name-wrap">
                        <span class="review-name">${this._escape(name)}<span class="review-verified" title="${this._escape(t('reviews_verified_patient', 'Подтверждённый пациент'))}">✓</span></span>
                        <div class="review-meta-row">
                            <span class="star-rating">${stars}</span>
                            <span class="review-date">${formatDate(r.createdAt)}</span>
                        </div>
                    </div>
                </div>
                <p class="review-text"><span class="review-quote-mark">&#8220;</span><span data-review-text-id="${r.id}" data-original-text="${this._escape(r.text)}">${this._escape(r.text)}</span></p>
                ${translateBtn}
            </div>
        `;
    }

    // Обработчик клика по кнопке "Перевести" на карточке отзыва.
    // Первый клик — переводит и превращает кнопку в "Показать оригинал".
    // Повторный клик — переключает обратно, без нового запроса к серверу
    // (оригинал всегда под рукой в data-original-text).
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
                    // Перевод не удался (например, кончилась квота API) — сообщаем
                    // об этом явно, а не молча оставляем оригинал без объяснения.
                    btn.innerHTML = `⚠️ ${t('review_translate_failed', 'Не удалось перевести')}`;
                    setTimeout(() => {
                        btn.innerHTML = `🌐 ${t('review_translate_btn', 'Перевести')}`;
                    }, 2500);
                }
            });
        });
    }

    _renderDots() {
        if (!this.dotsEl) return;
        if (this._totalPages <= 1) {
            this.dotsEl.innerHTML = '';
            return;
        }
        this.dotsEl.innerHTML = Array.from({ length: this._totalPages }, (_, i) =>
            `<button type="button" class="reviews-dot ${i === this.page ? 'active' : ''}" data-page="${i}" aria-label="${t('reviews_page_label', 'Страница {n}').replace('{n}', i + 1)}"></button>`
        ).join('');

        this.dotsEl.querySelectorAll('.reviews-dot').forEach(dot => {
            dot.addEventListener('click', () => this._slideTo(Number(dot.dataset.page)));
        });
    }

    _updateNavButtons() {
        const multiPage = this._totalPages > 1;
        if (this.prevBtn) this.prevBtn.style.display = multiPage ? '' : 'none';
        if (this.nextBtn) this.nextBtn.style.display = multiPage ? '' : 'none';
    }

    _setupCarouselNav() {
        this.prevBtn?.addEventListener('click', () => this._slideTo(this.page - 1));
        this.nextBtn?.addEventListener('click', () => this._slideTo(this.page + 1));
    }

    _slideTo(page, animate = true) {
        if (!this.trackEl || !this.trackWrap || !this._totalPages) return;

        this.page = ((page % this._totalPages) + this._totalPages) % this._totalPages;

        this.trackEl.style.transition = animate ? '' : 'none';
        const width = this.trackWrap.clientWidth;
        this.trackEl.style.transform = `translateX(-${this.page * width}px)`;
        if (!animate) {
            // форсируем применение стиля без анимации, затем возвращаем transition обратно
            void this.trackEl.offsetHeight;
            this.trackEl.style.transition = '';
        }

        this.dotsEl?.querySelectorAll('.reviews-dot').forEach((dot, i) =>
            dot.classList.toggle('active', i === this.page)
        );
    }

    // =========================
    // Кнопка "Оставить отзыв" + проверка авторизации
    // =========================
    _setupLeaveButton() {
        document.querySelectorAll('.btn-leave-review').forEach(btn => {
            btn.addEventListener('click', () => {
                const patientId = sessionStorage.getItem('patientId');
                const role = sessionStorage.getItem('userRole');

                if (patientId && role?.toLowerCase() === 'patient') {
                    this._resetForm();
                    if (this.reviewModal) this.reviewModal.style.display = 'block';
                } else {
                    if (this.loginRequiredModal) this.loginRequiredModal.style.display = 'block';
                }
            });
        });
    }

    _setupLoginRequiredModal() {
        if (!this.loginRequiredModal) return;

        this.loginRequiredModal.querySelectorAll('.close').forEach(btn =>
            btn.addEventListener('click', () => { this.loginRequiredModal.style.display = 'none'; })
        );
        this.loginRequiredModal.addEventListener('click', e => {
            if (e.target === this.loginRequiredModal) this.loginRequiredModal.style.display = 'none';
        });

        document.getElementById('review-login-required-login')?.addEventListener('click', () => {
            this.loginRequiredModal.style.display = 'none';
            document.querySelector('.btn-login')?.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        });
        document.getElementById('review-login-required-signup')?.addEventListener('click', () => {
            this.loginRequiredModal.style.display = 'none';
            document.querySelector('.btn-signup')?.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        });
    }

    // =========================
    // Форма отзыва
    // =========================
    _setupReviewModal() {
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
                body: JSON.stringify({ rating: this.selectedRating, text })
            });
            this._closeModal();
            showSuccess(t('review_submitted', 'Отзыв отправлен на проверку модератору'));
        } catch (err) {
            showError(err.message || t('review_submit_error', 'Не удалось отправить отзыв'));
        }
    }

    _escape(str) {
        const div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }
}

document.addEventListener('DOMContentLoaded', () => new PublicReviewsManager().init());

export { PublicReviewsManager };