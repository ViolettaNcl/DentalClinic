/**
 * siteMotion.js — site-wide interactive effects for public pages.
 *  - 3D tilt + cursor spotlight on cards (pointer devices only)
 *  - ripple + shine on buttons
 *  - floating particles in hero blocks
 *  - animated counters for the home-page stats
 *  - blur-in reveal for section titles
 * Everything is additive, safe for missing markup and disabled for
 * prefers-reduced-motion.
 */
(function () {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)').matches;

    const TILT_SELECTORS = [
        '.card',
        '.doctor-card',
        '.stat',
        '.review-card',
        '.ai-clinic-fact'
    ].join(',');

    const RIPPLE_SELECTORS = [
        '.btn-primary',
        '.route-build-btn',
        '.route-location-btn',
        '.route-reset-btn',
        '.smart-map-mode',
        '.route-external-link'
    ].join(',');

    /* ---------- 3D tilt + spotlight ---------- */
    function enhanceTilt(card) {
        if (card.dataset.motionTilt === '1') return;
        card.dataset.motionTilt = '1';
        card.classList.add('motion-tilt');

        if (getComputedStyle(card).position === 'static') card.style.position = 'relative';
        const spot = document.createElement('span');
        spot.className = 'motion-spot';
        spot.setAttribute('aria-hidden', 'true');
        card.appendChild(spot);

        const maxTilt = card.classList.contains('stat') ? 6 : 8;
        let frame = 0;

        card.addEventListener('pointermove', event => {
            if (event.pointerType !== 'mouse') return;
            cancelAnimationFrame(frame);
            frame = requestAnimationFrame(() => {
                const rect = card.getBoundingClientRect();
                const x = (event.clientX - rect.left) / rect.width;
                const y = (event.clientY - rect.top) / rect.height;
                card.style.setProperty('--ry', `${(x - 0.5) * maxTilt * 2}deg`);
                card.style.setProperty('--rx', `${(0.5 - y) * maxTilt * 2}deg`);
                card.style.setProperty('--mx', `${x * 100}%`);
                card.style.setProperty('--my', `${y * 100}%`);
                card.classList.add('is-tilting');
            });
        });

        card.addEventListener('pointerleave', () => {
            cancelAnimationFrame(frame);
            card.classList.remove('is-tilting');
            card.style.setProperty('--rx', '0deg');
            card.style.setProperty('--ry', '0deg');
        });
    }

    /* ---------- Button ripple (delegated, works for re-rendered buttons) ---------- */
    function installRipple() {
        document.addEventListener('pointerdown', event => {
            const host = event.target.closest(RIPPLE_SELECTORS);
            if (!host || host.disabled) return;

            host.classList.add('motion-ripple-host');
            const rect = host.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const ripple = document.createElement('span');
            ripple.className = 'motion-ripple';
            ripple.setAttribute('aria-hidden', 'true');
            ripple.style.width = ripple.style.height = `${size}px`;
            ripple.style.left = `${event.clientX - rect.left - size / 2}px`;
            ripple.style.top = `${event.clientY - rect.top - size / 2}px`;
            host.appendChild(ripple);
            ripple.addEventListener('animationend', () => ripple.remove(), { once: true });
        }, { passive: true });
    }

    function enhanceShine(button) {
        if (button.querySelector(':scope > .motion-shine')) return;
        const shine = document.createElement('span');
        shine.className = 'motion-shine';
        shine.setAttribute('aria-hidden', 'true');
        button.appendChild(shine);
    }

    /* ---------- Hero particles ---------- */
    function addParticles(hero, count) {
        if (!hero || hero.querySelector('.motion-particles') || window.innerWidth < 600) return;
        if (getComputedStyle(hero).position === 'static') hero.style.position = 'relative';

        const layer = document.createElement('div');
        layer.className = 'motion-particles';
        layer.setAttribute('aria-hidden', 'true');
        const symbols = ['✦', '✧', '⋆', '•', '✦'];
        for (let i = 0; i < count; i += 1) {
            const p = document.createElement('span');
            p.className = 'motion-particle';
            p.textContent = symbols[i % symbols.length];
            p.style.left = `${3 + Math.random() * 94}%`;
            p.style.setProperty('--p-size', `${10 + Math.random() * 14}px`);
            p.style.setProperty('--p-duration', `${9 + Math.random() * 9}s`);
            p.style.setProperty('--p-delay', `${Math.random() * -16}s`);
            p.style.setProperty('--p-drift', `${Math.random() * 80 - 40}px`);
            layer.appendChild(p);
        }
        hero.appendChild(layer);
    }

    /* ---------- Stats counters ("150+", "2") ---------- */
    function animateCounter(el) {
        const original = el.textContent.trim();
        const match = original.match(/^(\d+)(.*)$/);
        if (!match) return;
        const target = Number(match[1]);
        const suffix = match[2];
        if (!Number.isFinite(target) || target <= 0) return;

        el.classList.add('motion-counting');
        const duration = 1400;
        const start = performance.now();
        const tick = now => {
            const progress = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - progress, 3);
            el.textContent = `${Math.round(target * eased)}${suffix}`;
            if (progress < 1) requestAnimationFrame(tick);
            else el.textContent = original;
        };
        requestAnimationFrame(tick);
    }

    function installCounters() {
        const counters = [...document.querySelectorAll('.stats .stat h3')]
            .filter(el => !el.id); // dynamic values (e.g. rating) are filled by the API
        if (!counters.length || !('IntersectionObserver' in window)) return;

        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                observer.unobserve(entry.target);
                animateCounter(entry.target);
            });
        }, { threshold: 0.6 });
        counters.forEach(el => observer.observe(el));
    }

    /* ---------- Hero: glow following the cursor + soft parallax ---------- */
    function installHeroGlow(hero) {
        if (!hero || !finePointer || hero.querySelector('.motion-hero-glow')) return;
        const glow = document.createElement('div');
        glow.className = 'motion-hero-glow';
        glow.setAttribute('aria-hidden', 'true');
        hero.appendChild(glow);
        const parallax = hero.classList.contains('hero');
        if (parallax) hero.classList.add('motion-parallax');

        let frame = 0;
        hero.addEventListener('pointermove', event => {
            cancelAnimationFrame(frame);
            frame = requestAnimationFrame(() => {
                const rect = hero.getBoundingClientRect();
                const x = (event.clientX - rect.left) / rect.width;
                const y = (event.clientY - rect.top) / rect.height;
                glow.style.setProperty('--gx', `${x * 100}%`);
                glow.style.setProperty('--gy', `${y * 100}%`);
                glow.classList.add('is-on');
                if (parallax) {
                    hero.style.setProperty('--px', `${(0.5 - x) * 20}px`);
                    hero.style.setProperty('--py', `${(0.5 - y) * 14}px`);
                }
            });
        });
        hero.addEventListener('pointerleave', () => {
            glow.classList.remove('is-on');
            hero.style.setProperty('--px', '0px');
            hero.style.setProperty('--py', '0px');
        });
    }

    /* ---------- Magnetic call-to-action buttons ---------- */
    function enhanceMagnetic(button) {
        if (!finePointer || button.dataset.motionMagnetic === '1') return;
        button.dataset.motionMagnetic = '1';
        button.classList.add('motion-magnetic');
        button.addEventListener('pointermove', event => {
            const rect = button.getBoundingClientRect();
            const dx = event.clientX - (rect.left + rect.width / 2);
            const dy = event.clientY - (rect.top + rect.height / 2);
            button.style.setProperty('--tx', `${dx * 0.18}px`);
            button.style.setProperty('--ty', `${dy * 0.28}px`);
            button.classList.add('is-pulled');
        });
        button.addEventListener('pointerleave', () => button.classList.remove('is-pulled'));
    }

    /* ---------- Photos reveal like a curtain when scrolled into view ---------- */
    const imageObserver = 'IntersectionObserver' in window
        ? new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-revealed');
                imageObserver.unobserve(entry.target);
            });
        }, { threshold: 0.15 })
        : null;

    function enhanceImage(img) {
        if (!imageObserver || img.dataset.motionImg === '1') return;
        img.dataset.motionImg = '1';
        // Already on screen at load: do not hide it.
        const rect = img.getBoundingClientRect();
        if (rect.top < window.innerHeight && rect.bottom > 0) return;
        img.classList.add('motion-img-reveal');
        imageObserver.observe(img);
    }

    /* ---------- Smooth fade when leaving to another page of the site ---------- */
    function installPageLeave() {
        document.addEventListener('click', event => {
            const link = event.target.closest('a[href]');
            if (!link || event.defaultPrevented || event.button !== 0
                || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
            if (link.target && link.target !== '_self') return;
            const href = link.getAttribute('href');
            if (!href || href.startsWith('#') || href.startsWith('mailto:') || href.startsWith('tel:')) return;
            const url = new URL(link.href, location.href);
            if (url.origin !== location.origin || (url.pathname === location.pathname && url.hash)) return;
            if (!/\.html$|\/$/.test(url.pathname)) return;

            event.preventDefault();
            document.body.classList.add('motion-leaving');
            setTimeout(() => { location.href = url.href; }, 200);
        });
        // Back/forward cache restores the faded page — show it again.
        window.addEventListener('pageshow', () => document.body.classList.remove('motion-leaving'));
    }

    /* ---------- Enhance current + future DOM ---------- */
    function enhance(root = document) {
        if (finePointer) root.querySelectorAll(TILT_SELECTORS).forEach(enhanceTilt);
        root.querySelectorAll('.btn-primary, .route-build-btn').forEach(enhanceShine);
        root.querySelectorAll('.hero .btn-primary, .contact-form button[type="submit"], .route-build-btn')
            .forEach(enhanceMagnetic);
        root.querySelectorAll('.doctor-card__photo img, .philosophy-image img, .about-block img, .gallery-item img')
            .forEach(enhanceImage);
    }

    function init() {
        document.body.classList.add('motion-ready');
        if (reduceMotion) return;

        installRipple();
        installCounters();
        addParticles(document.querySelector('.hero'), 14);
        addParticles(document.querySelector('.contact-hero'), 10);
        installHeroGlow(document.querySelector('.hero'));
        installHeroGlow(document.querySelector('.contact-hero'));
        installPageLeave();
        enhance();

        // Header, footer, doctor cards and reviews are rendered asynchronously.
        let scheduled = false;
        const isOwnNode = node => node.nodeType !== 1
            || /motion-(ripple|spot|shine|particle)/.test(node.className || '');
        const observer = new MutationObserver(mutations => {
            const relevant = mutations.some(m => [...m.addedNodes].some(node => !isOwnNode(node)));
            if (!relevant || scheduled) return;
            scheduled = true;
            requestAnimationFrame(() => {
                scheduled = false;
                enhance();
            });
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
