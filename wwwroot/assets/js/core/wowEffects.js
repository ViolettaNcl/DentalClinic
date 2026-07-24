/**
 * wowEffects.js
 * — Индикатор прогресса чтения (initProgressBar) работает на ЛЮБОЙ странице
 *   сайта, где подключён этот файл (главная, about, contact, doctors,
 *   admin-dashboard, patient-dashboard и т.д.).
 * — Остальные эффекты (частицы, наклон карточек, FAQ-рябь, магнитные кнопки)
 *   работают только внутри <main class="service-detail-page"> —
 *   то есть только на 8 страницах услуг.
 * Ничего не ломает, если разметка отличается — все выборки безопасные.
 */
(function () {
    const page = document.querySelector('.service-detail-page');
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    /* ---------- 1. Индикатор прогресса чтения ---------- */
    function initProgressBar() {
        const bar = document.createElement('div');
        bar.className = 'scroll-progress-bar';
        document.body.prepend(bar);

        function update() {
            const scrollTop = window.scrollY;
            const docHeight = document.documentElement.scrollHeight - window.innerHeight;
            const pct = docHeight > 0 ? (scrollTop / docHeight) * 100 : 0;
            bar.style.width = Math.min(100, Math.max(0, pct)) + '%';
        }
        update();
        window.addEventListener('scroll', update, { passive: true });
        window.addEventListener('resize', update);
    }

    /* ---------- 2. Плавающие частицы в hero ---------- */
    function initHeroParticles() {
        if (reduceMotion) return;
        const hero = page.querySelector('.hero-section');
        if (!hero) return;
        if (window.innerWidth < 600) return;

        const symbols = ['✦', '✧', '⋆', '•'];
        const count = 10;
        for (let i = 0; i < count; i++) {
            const p = document.createElement('span');
            p.className = 'wow-particle';
            p.textContent = symbols[i % symbols.length];
            p.style.left = (4 + Math.random() * 92) + '%';
            p.style.setProperty('--p-size', (12 + Math.random() * 16) + 'px');
            p.style.setProperty('--p-duration', (8 + Math.random() * 8) + 's');
            p.style.setProperty('--p-delay', (Math.random() * -14) + 's');
            p.style.setProperty('--p-drift', (Math.random() * 60 - 30) + 'px');
            hero.appendChild(p);
        }
    }

    /* ---------- 3. 3D-наклон карточек по курсору ---------- */
    function initTilt() {
        if (reduceMotion) return;
        const targets = page.querySelectorAll('.type-card, .gallery-item, .card--pricing');
        targets.forEach(card => {
            card.addEventListener('mousemove', e => {
                const rect = card.getBoundingClientRect();
                const px = (e.clientX - rect.left) / rect.width;
                const py = (e.clientY - rect.top) / rect.height;
                const ry = (px - 0.5) * 10;   // влево-вправо
                const rx = (0.5 - py) * 8;    // вверх-вниз
                card.style.setProperty('--rx', rx.toFixed(2) + 'deg');
                card.style.setProperty('--ry', ry.toFixed(2) + 'deg');
                card.style.setProperty('--mx', (px * 100).toFixed(1) + '%');
                card.style.setProperty('--my', (py * 100).toFixed(1) + '%');
            });
            card.addEventListener('mouseleave', () => {
                card.style.setProperty('--rx', '0deg');
                card.style.setProperty('--ry', '0deg');
            });
        });
    }

    /* ---------- 4. Появление галереи + счётчик цен при скролле ---------- */
    function initScrollTriggers() {
        const galleryItems = page.querySelectorAll('.gallery-item');
        const priceCards = page.querySelectorAll('.card--pricing');

        if (!('IntersectionObserver' in window)) {
            galleryItems.forEach(el => el.classList.add('wow-in-view'));
            priceCards.forEach(el => el.classList.add('wow-counted'));
            return;
        }

        const galleryObserver = new IntersectionObserver((entries) => {
            entries.forEach((entry, i) => {
                if (entry.isIntersecting) {
                    setTimeout(() => entry.target.classList.add('wow-in-view'), i % 4 * 90);
                    galleryObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.15 });
        galleryItems.forEach(el => galleryObserver.observe(el));

        const priceObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    animateAmount(entry.target);
                    entry.target.classList.add('wow-counted');
                    priceObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.35 });
        priceCards.forEach(el => priceObserver.observe(el));
    }

    function animateAmount(card) {
        const amountEl = card.querySelector('.card__price .amount');
        if (!amountEl) return;
        const raw = amountEl.textContent.replace(/\s|\u00A0/g, '');
        const target = parseInt(raw, 10);
        if (!target || isNaN(target)) return;
        if (reduceMotion) return;

        const duration = 900;
        const start = performance.now();

        function frame(now) {
            const t = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - t, 3);
            const value = Math.round(target * eased);
            amountEl.textContent = value.toLocaleString('ru-RU');
            if (t < 1) requestAnimationFrame(frame);
            else amountEl.textContent = target.toLocaleString('ru-RU');
        }
        requestAnimationFrame(frame);
    }

    /* ---------- 5. Рябь при клике на вопрос FAQ ---------- */
    function initFaqRipple() {
        page.querySelectorAll('.faq-question').forEach(btn => {
            btn.addEventListener('click', e => {
                const rect = btn.getBoundingClientRect();
                const ripple = document.createElement('span');
                const size = Math.max(rect.width, rect.height) * 1.4;
                ripple.className = 'wow-ripple';
                ripple.style.width = ripple.style.height = size + 'px';
                ripple.style.left = (e.clientX - rect.left - size / 2) + 'px';
                ripple.style.top = (e.clientY - rect.top - size / 2) + 'px';
                btn.appendChild(ripple);
                ripple.addEventListener('animationend', () => ripple.remove());
            });
        });
    }

    /* ---------- 6. Лёгкий "магнитный" эффект для кнопок ---------- */
    function initMagneticButtons() {
        if (reduceMotion) return;
        page.querySelectorAll('.btn-primary, .btn-cta-white').forEach(btn => {
            btn.addEventListener('mousemove', e => {
                const rect = btn.getBoundingClientRect();
                const x = (e.clientX - rect.left - rect.width / 2) * 0.18;
                const y = (e.clientY - rect.top - rect.height / 2) * 0.28;
                btn.style.transform = `translate(${x}px, ${y}px)`;
            });
            btn.addEventListener('mouseleave', () => {
                btn.style.transform = '';
            });
        });
    }

    function start() {
        // Прогресс-бар — глобальный эффект, работает на любой странице
        initProgressBar();

        // Всё остальное — только на страницах услуг (.service-detail-page)
        if (!page) return;
        initHeroParticles();
        initTilt();
        initScrollTriggers();
        initFaqRipple();
        initMagneticButtons();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();