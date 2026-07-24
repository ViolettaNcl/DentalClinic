// =====================================================
// 🔀 НАВИГАЦИЯ И UI (мобильное меню + подсветка + стрелка)
// =====================================================

class NavigationManager {
    constructor() {
        this.hamburger = document.querySelector('.hamburger');
        this.navMenu = document.querySelector('nav ul');
        this.headerButtons = document.querySelector('.header-buttons');
        this.servicesMenu = document.querySelector('.services-menu');
        this.servicesDropdown = this.servicesMenu?.querySelector('.dropdown');
    }

    init() {
        this.initHamburger();
        this.initServicesDropdown();
        this.initActiveLinks();      // ← подсветка включена
        this.initScrollTopButton();  // ← стрелка наверх включена
        this.initHeaderScrollEffect(); // ← стеклянный хедер при скролле
    }

    // ===== Эффект хедера при прокрутке =====
    initHeaderScrollEffect() {
        const header = document.querySelector('header');
        if (!header) return;
        const update = () => header.classList.toggle('scrolled', window.scrollY > 30);
        update();
        window.addEventListener('scroll', update, { passive: true });
    }

    // ===== Мобильное меню (гамбургер) =====
    initHamburger() {
        const { hamburger, navMenu, headerButtons } = this;
        if (!hamburger || !navMenu) return;

        hamburger.addEventListener('click', () => {
            const isOpen = navMenu.classList.toggle('active');
            hamburger.classList.toggle('open', isOpen);
            headerButtons?.classList.toggle('active', isOpen);
            document.documentElement.style.overflow = isOpen ? 'hidden' : '';
        });
    }

    // ===== Подсветка активной ссылки =====
    initActiveLinks() {
        const links = document.querySelectorAll('nav a');
        if (!links.length) return;

        const currentPage = (location.pathname.split('/').pop() || 'index.html').toLowerCase();

        links.forEach(a => {
            const href = (a.getAttribute('href') || '').split('/').pop().toLowerCase();
            a.classList.toggle('active', href === currentPage);
        });
    }

    // ===== Подменю "Услуги" (только ПК) =====
    initServicesDropdown() {
        const { servicesMenu, servicesDropdown } = this;
        if (!servicesMenu || !servicesDropdown) return;

        const toggle = show =>
            window.innerWidth > 768 && servicesDropdown.classList.toggle('active', show);

        servicesMenu.addEventListener('mouseenter', () => toggle(true));
        servicesMenu.addEventListener('mouseleave', () => toggle(false));

        document.addEventListener('click', e => {
            if (!servicesMenu.contains(e.target)) servicesDropdown.classList.remove('active');
        });
    }

    // ===== Кнопка "Наверх" =====
    initScrollTopButton() {
        const btn = document.createElement('button');
        btn.className = 'scroll-to-top';
        btn.ariaLabel = 'Наверх';
        document.body.appendChild(btn);

        btn.onclick = () => window.scrollTo({ top: 0, behavior: 'smooth' });

        window.addEventListener('scroll', () => {
            btn.classList.toggle('visible', window.scrollY > 300);
        });
    }
}

export { NavigationManager };