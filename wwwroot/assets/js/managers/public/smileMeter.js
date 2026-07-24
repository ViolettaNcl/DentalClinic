/**
 * smileMeter.js — «Улыбкометр» с реалистичной улыбкой (v3.2, с i18n).
 * Три ползунка (белизна, ровность, форма) независимо смешивают 8 фото-слоёв
 * методом трилинейной интерполяции. Все динамические тексты (рекомендации,
 * hero-подписи) теперь берутся из словаря переводов и обновляются
 * автоматически при смене языка.
 *
 * ВАЖНО: подключать скрипт нужно с type="module":
 *   <script type="module" src="/assets/js/managers/public/smileMeter.js"></script>
 */
import { t, onLanguageChange, ready } from '../../core/i18n.js';

(async function () {
    const root = document.getElementById('smile-meter');
    if (!root) return;

    const section = document.querySelector('.smile-meter-section');

    const sliderWhite = document.getElementById('slider-white');
    const sliderAlign = document.getElementById('slider-align');
    const sliderShape = document.getElementById('slider-shape');

    const photoStack = document.getElementById('smile-photo-stack');

    const ringProgress = document.getElementById('smile-ring-progress');
    const scoreValueEl = document.getElementById('smile-score-value');
    const heroLabelEl = document.getElementById('smile-hero-label');
    const confettiHost = document.getElementById('smile-confetti-host');

    const resultIcon = document.getElementById('smile-result-icon');
    const resultText = document.getElementById('smile-result-text');
    const resultLink = document.getElementById('smile-result-link');

    if (!sliderWhite || !sliderAlign || !sliderShape || !photoStack) return;

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const RING_RADIUS = 122;
    const RING_CIRC = 2 * Math.PI * RING_RADIUS;

    const SLIDER_COLORS = { white: '#13B39B', align: '#FF9843', shape: '#5FC9E8' };
    const CONFETTI_COLORS = ['#13B39B', '#FF9843', '#FFD166', '#5FC9E8', '#FF6B8B'];

    // Порядок слоёв в DOM: 000 → 111. Индекс = w*4 + a*2 + s.
    const layers = Array.from(photoStack.querySelectorAll('[data-corner]'));

    let lastScore = -1;
    let scoreDisplayed = 0;
    let rafId = null;
    let snapTimer = null;
    let lastWeakestKey = null; // чтобы перерисовать текст рекомендации при смене языка

    const SNAP_DELAY = 220; // мс тишины после отпускания слайдера, затем — "доводка" до чёткого фото

    if (ringProgress) {
        ringProgress.setAttribute('stroke-dasharray', RING_CIRC.toFixed(1));
        ringProgress.setAttribute('stroke-dashoffset', RING_CIRC.toFixed(1));
    }

    /* ---------- Тексты рекомендаций — берутся из словаря переводов ---------- */
    function getRecommend() {
        return {
            white: {
                icon: '✨',
                text: t('smile_rec_white_text', 'Больше всего улыбке не хватает белизны. Профессиональная эстетика поможет добиться сияющего результата.'),
                label: t('smile_rec_white_label', 'Косметическая стоматология'),
                href: '/pages/services/cosmetic-treatments.html'
            },
            align: {
                icon: '📐',
                text: t('smile_rec_align_text', 'Ровность зубов сильнее всего влияет на впечатление от улыбки. Виниры и коронки способны визуально выровнять зубной ряд.'),
                label: t('smile_rec_align_label', 'Коронки и виниры'),
                href: '/pages/services/crowns.html'
            },
            shape: {
                icon: '💫',
                text: t('smile_rec_shape_text', 'Форма улыбки станет выразительнее с правильной эстетической реставрацией.'),
                label: t('smile_rec_shape_label', 'Косметическая стоматология'),
                href: '/pages/services/cosmetic-treatments.html'
            }
        };
    }

    /**
     * Трилинейное смешивание 8 фото — используется как "живой" превью
     * во время движения ползунка.
     */
    function computeWeights(white01, align01, shape01) {
        const axes = [
            [1 - white01, white01],
            [1 - align01, align01],
            [1 - shape01, shape01]
        ];
        const weights = new Array(8);
        for (let w = 0; w < 2; w++) {
            for (let a = 0; a < 2; a++) {
                for (let s = 0; s < 2; s++) {
                    weights[w * 4 + a * 2 + s] = axes[0][w] * axes[1][a] * axes[2][s];
                }
            }
        }
        return weights;
    }

    function renderBlended(weights) {
        let cumulative = 0;
        for (let i = 0; i < layers.length; i++) {
            const wi = weights[i] || 0;
            cumulative += wi;
            const alpha = cumulative > 0 ? wi / cumulative : (i === 0 ? 1 : 0);
            layers[i].style.opacity = alpha.toFixed(4);
        }
    }

    /**
     * "Доводка" — показать ОДНО ближайшее по весу фото на 100%,
     * все остальные скрыть. Устраняет двоение после остановки слайдера.
     */
    function snapToNearest(weights) {
        let bestIdx = 0;
        let bestWeight = -1;
        for (let i = 0; i < weights.length; i++) {
            if (weights[i] > bestWeight) {
                bestWeight = weights[i];
                bestIdx = i;
            }
        }
        layers.forEach((el, i) => {
            el.style.opacity = i === bestIdx ? '1' : '0';
        });
    }

    function renderPhoto(white01, align01, shape01, immediate) {
        const weights = computeWeights(white01, align01, shape01);

        if (snapTimer) clearTimeout(snapTimer);

        if (reduceMotion || immediate) {
            snapToNearest(weights);
            return;
        }

        renderBlended(weights);
        snapTimer = setTimeout(() => snapToNearest(weights), SNAP_DELAY);
    }

    function updateRing(score) {
        if (!ringProgress) return;
        const offset = RING_CIRC * (1 - score / 100);
        ringProgress.setAttribute('stroke-dashoffset', offset.toFixed(1));
    }

    function animateScoreCount(target) {
        if (rafId) cancelAnimationFrame(rafId);
        if (reduceMotion) {
            scoreDisplayed = target;
            if (scoreValueEl) scoreValueEl.textContent = target;
            updateRing(target);
            return;
        }
        const from = scoreDisplayed;
        const duration = 420;
        const start = performance.now();

        function frame(now) {
            const t2 = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - t2, 3);
            const value = Math.round(from + (target - from) * eased);
            scoreDisplayed = value;
            if (scoreValueEl) scoreValueEl.textContent = value;
            updateRing(value);
            if (t2 < 1) rafId = requestAnimationFrame(frame);
        }
        rafId = requestAnimationFrame(frame);
    }

    function burstConfetti() {
        if (reduceMotion || !confettiHost) return;
        const count = 22;
        for (let i = 0; i < count; i++) {
            const piece = document.createElement('span');
            piece.className = 'smile-confetti-piece';
            const angle = Math.random() * Math.PI * 2;
            const dist = 70 + Math.random() * 90;
            piece.style.setProperty('--cx', Math.cos(angle) * dist + 'px');
            piece.style.setProperty('--cy', Math.sin(angle) * dist + 'px');
            piece.style.setProperty('--cr', (Math.random() * 360) + 'deg');
            piece.style.background = CONFETTI_COLORS[i % CONFETTI_COLORS.length];
            piece.style.animationDelay = (Math.random() * 0.08) + 's';
            confettiHost.appendChild(piece);
            piece.addEventListener('animationend', () => piece.remove());
        }
    }

    function popHeroLabel(textValue) {
        if (!heroLabelEl) return;
        heroLabelEl.textContent = textValue;
        heroLabelEl.classList.remove('pop');
        void heroLabelEl.offsetWidth;
        heroLabelEl.classList.add('pop');
    }

    function updateSliderVisuals() {
        [
            [sliderWhite, 'white'],
            [sliderAlign, 'align'],
            [sliderShape, 'shape']
        ].forEach(([slider, key]) => {
            slider.style.setProperty('--val', slider.value + '%');
            slider.style.setProperty('--slider-color', SLIDER_COLORS[key]);
            const valueEl = slider.closest('.smile-slider')?.querySelector('.smile-slider-value');
            if (valueEl) valueEl.textContent = slider.value + '%';
        });
    }

    function update() {
        const white = parseInt(sliderWhite.value, 10);
        const align = parseInt(sliderAlign.value, 10);
        const shape = parseInt(sliderShape.value, 10);

        updateSliderVisuals();
        renderPhoto(white / 100, align / 100, shape / 100);

        const score = Math.round((white + align + shape) / 3);
        animateScoreCount(score);

        const values = { white, align, shape };
        const weakestKey = Object.keys(values).reduce((a, b) => values[a] <= values[b] ? a : b);
        lastWeakestKey = weakestKey;

        const RECOMMEND = getRecommend();
        const rec = RECOMMEND[weakestKey];
        const isGreat = score >= 90;

        if (resultIcon) resultIcon.textContent = isGreat ? '🏆' : rec.icon;
        if (resultText) {
            resultText.textContent = isGreat
                ? t('smile_rec_great_text', 'Отличный результат — почти голливудская улыбка! Осталось только закрепить её у профессионалов.')
                : rec.text;
            resultText.classList.remove('swap');
            void resultText.offsetWidth;
            resultText.classList.add('swap');
        }
        if (resultLink) {
            resultLink.textContent = `${rec.label} →`;
            resultLink.href = rec.href;
        }

        if (score >= 90 && lastScore < 90) {
            burstConfetti();
            popHeroLabel(t('smile_hero_hollywood', 'Голливудская улыбка! 🏆'));
        } else if (score >= 70 && lastScore < 70) {
            popHeroLabel(t('smile_hero_almost', 'Уже почти идеально ✨'));
        } else if (score < 70) {
            if (heroLabelEl) heroLabelEl.textContent = '';
        }
        lastScore = score;
    }

    /**
     * Перерисовать только текстовые части при смене языка, не трогая
     * фото/слайдеры/счёт — чтобы не было лишних анимаций/конфетти.
     */
    function refreshTextsOnLanguageChange() {
        if (lastWeakestKey === null) return;
        const RECOMMEND = getRecommend();
        const rec = RECOMMEND[lastWeakestKey];
        const isGreat = lastScore >= 90;

        if (resultText) {
            resultText.textContent = isGreat
                ? t('smile_rec_great_text', 'Отличный результат — почти голливудская улыбка! Осталось только закрепить её у профессионалов.')
                : rec.text;
        }
        if (resultLink) {
            resultLink.textContent = `${rec.label} →`;
        }
        if (heroLabelEl && heroLabelEl.textContent) {
            if (lastScore >= 90) heroLabelEl.textContent = t('smile_hero_hollywood', 'Голливудская улыбка! 🏆');
            else if (lastScore >= 70) heroLabelEl.textContent = t('smile_hero_almost', 'Уже почти идеально ✨');
        }
    }

    await ready; // дожидаемся загрузки словаря текущего языка перед первым рендером

    [sliderWhite, sliderAlign, sliderShape].forEach(s => s.addEventListener('input', update));
    update();

    // сразу после начальной загрузки — чёткое фото без плавного захода
    renderPhoto(sliderWhite.value / 100, sliderAlign.value / 100, sliderShape.value / 100, true);

    onLanguageChange(() => refreshTextsOnLanguageChange());

    if (section && 'IntersectionObserver' in window) {
        const io = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    section.classList.add('in-view');
                    io.unobserve(entry.target);
                }
            });
        }, { threshold: 0.25 });
        io.observe(section);
    } else if (section) {
        section.classList.add('in-view');
    }
})();