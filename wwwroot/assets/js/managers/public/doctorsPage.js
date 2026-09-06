/**
 * Анимации страницы "Врачи":
 *  1. Плавный 3D-наклон карточки вслед за курсором мыши.
 *  2. Анимированный счётчик цифр в блоке статистики при появлении на экране.
 *  3. Каскадное появление списка навыков (класс .in-view подключает CSS-анимацию).
 */
(function () {
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const isFinePointer = window.matchMedia('(pointer: fine)').matches;

    function init() {
        const cards = document.querySelectorAll('.doctor-card');
        if (!cards.length) return;

        // ===== 1. 3D-наклон карточки за курсором =====
        if (isFinePointer && !prefersReducedMotion) {
            cards.forEach(card => {
                const maxTilt = 4; // градусы — эффект лёгкий и деликатный

                card.addEventListener('mousemove', (e) => {
                    const rect = card.getBoundingClientRect();
                    const x = (e.clientX - rect.left) / rect.width;  // 0..1
                    const y = (e.clientY - rect.top) / rect.height;  // 0..1
                    const rotateY = (x - 0.5) * maxTilt * 2;
                    const rotateX = (0.5 - y) * maxTilt * 2;
                    card.style.transform = `perspective(1200px) rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
                });

                card.addEventListener('mouseleave', () => {
                    card.style.transform = 'perspective(1200px) rotateX(0deg) rotateY(0deg)';
                });
            });
        }

        // ===== 2 и 3: счётчики + каскад навыков при появлении в зоне видимости =====
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                const card = entry.target;
                card.classList.add('in-view');
                animateStats(card);
                observer.unobserve(card);
            });
        }, { threshold: 0.25 });

        cards.forEach(card => observer.observe(card));
    }

    function animateStats(card) {
        const numbers = card.querySelectorAll('.stat-number');
        numbers.forEach(el => {
            const raw = el.textContent.trim();
            const match = raw.match(/^(\d+)(.*)$/); // например "50+" → 50 и "+"
            if (!match) return; // например "5/5" — оставляем как есть

            const target = parseInt(match[1], 10);
            const suffix = match[2] || '';

            if (prefersReducedMotion) {
                el.textContent = target + suffix;
                return;
            }

            const duration = 2200;
            const start = performance.now();

            function tick(now) {
                const progress = Math.min((now - start) / duration, 1);
                const eased = 1 - Math.pow(1 - progress, 3); // ease-out cubic
                const value = Math.round(target * eased);
                el.textContent = value + suffix;
                if (progress < 1) requestAnimationFrame(tick);
            }

            requestAnimationFrame(tick);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();