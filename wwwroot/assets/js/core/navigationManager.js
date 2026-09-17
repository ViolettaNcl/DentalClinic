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
        this.initActiveLinks();
        this.initScrollTopButton();
        this.initHeaderScrollEffect();
    }

    initHeaderScrollEffect() {
        const header = document.querySelector('header');
        if (!header) return;
        const update = () => header.classList.toggle('scrolled', window.scrollY > 30);
        update();
        window.addEventListener('scroll', update, { passive: true });
    }

    initHamburger() {
        const { hamburger, navMenu, headerButtons } = this;
        if (!hamburger || !navMenu) return;

        const applyState = isOpen => {
            navMenu.classList.toggle('active', isOpen);
            hamburger.classList.toggle('open', isOpen);
            hamburger.setAttribute('aria-expanded', String(isOpen));
            headerButtons?.classList.toggle('active', isOpen);
            document.documentElement.classList.toggle('mobile-menu-open', isOpen);
            document.documentElement.style.overflow = isOpen ? 'hidden' : '';
        };

        hamburger.setAttribute('role', 'button');
        hamburger.setAttribute('tabindex', '0');
        hamburger.setAttribute('aria-label', 'Меню');
        hamburger.setAttribute('aria-expanded', 'false');

        const toggle = () => applyState(!navMenu.classList.contains('active'));
        hamburger.addEventListener('click', toggle);
        hamburger.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); toggle(); }
        });

        navMenu.querySelectorAll('a').forEach(link => link.addEventListener('click', () => applyState(false)));
        headerButtons?.querySelectorAll('a').forEach(link => link.addEventListener('click', () => applyState(false)));

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && navMenu.classList.contains('active')) applyState(false);
        });

        window.addEventListener('resize', () => {
            if (window.innerWidth > 992 && navMenu.classList.contains('active')) applyState(false);
        }, { passive: true });
    }

    initActiveLinks() {
        const links = document.querySelectorAll('nav a');
        if (!links.length) return;
        const currentPage = (location.pathname.split('/').pop() || 'index.html').toLowerCase();
        links.forEach(a => {
            const href = (a.getAttribute('href') || '').split('/').pop().toLowerCase();
            a.classList.toggle('active', href === currentPage);
        });
    }

    initServicesDropdown() {
        const { servicesMenu, servicesDropdown } = this;
        if (!servicesMenu || !servicesDropdown) return;
        const toggle = show => window.innerWidth > 992 && servicesDropdown.classList.toggle('active', show);
        servicesMenu.addEventListener('mouseenter', () => toggle(true));
        servicesMenu.addEventListener('mouseleave', () => toggle(false));
        document.addEventListener('click', e => {
            if (!servicesMenu.contains(e.target)) servicesDropdown.classList.remove('active');
        });
    }

    initScrollTopButton() {
        const btn = document.createElement('button');
        btn.className = 'scroll-to-top';
        btn.ariaLabel = 'Наверх';
        document.body.appendChild(btn);
        btn.onclick = () => window.scrollTo({ top: 0, behavior: 'smooth' });
        window.addEventListener('scroll', () => btn.classList.toggle('visible', window.scrollY > 300), { passive: true });
    }
}

export { NavigationManager };
