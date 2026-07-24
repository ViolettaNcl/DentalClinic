/**
 * Лёгкая утилита для анимации появления элементов при прокрутке.
 * Не требует изменения контента — сама находит секции, карточки и блоки
 * на странице и добавляет им плавное появление.
 */
(function () {
    const AUTO_SELECTORS = [
        '.card',
        '.faq-item',
        '.about-block',
        '.philosophy-image',
        '.philosophy-text',
        '.tech-card',
        '.section-title',
        '.certificates-section .card-grid',
        '.doctor-card',
        '.contact-block',
        '.contact-card'
    ];

    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function init() {
        if (prefersReducedMotion()) return;

        const found = new Set();
        AUTO_SELECTORS.forEach(sel => {
            document.querySelectorAll(sel).forEach(el => found.add(el));
        });

        // Не трогаем уже размеченные вручную элементы
        document.querySelectorAll('[data-reveal]').forEach(el => found.add(el));

        if (!found.size) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry, i) => {
                if (entry.isIntersecting) {
                    const el = entry.target;
                    const delay = Math.min(parseInt(el.dataset.revealIndex || 0, 10) * 70, 350);
                    setTimeout(() => el.classList.add('reveal-visible'), delay);
                    observer.unobserve(el);
                }
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -40px 0px' });

        let groupIndex = 0;
        let lastParent = null;
        found.forEach(el => {
            if (!el.hasAttribute('data-reveal')) {
                el.setAttribute('data-reveal', '');
            }
            if (el.parentElement !== lastParent) {
                groupIndex = 0;
                lastParent = el.parentElement;
            }
            el.dataset.revealIndex = groupIndex++;
            observer.observe(el);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
