const layout = document.querySelector('.panel-layout');
const sidebar = document.querySelector('.panel-sidebar');
const toggle = document.querySelector('.panel-mobile-nav-toggle');
const backdrop = document.querySelector('.panel-sidebar-backdrop');
if (layout && sidebar && toggle && backdrop) {
  const close = (restoreFocus = false) => {
    layout.classList.remove('is-nav-open');
    toggle.setAttribute('aria-expanded', 'false');
    backdrop.hidden = true;
    if (restoreFocus) toggle.focus();
  };
  const open = () => {
    layout.classList.add('is-nav-open');
    toggle.setAttribute('aria-expanded', 'true');
    backdrop.hidden = false;
    const first = sidebar.querySelector('button,a,[tabindex]:not([tabindex="-1"])');
    requestAnimationFrame(() => first?.focus());
  };
  toggle.addEventListener('click', () => layout.classList.contains('is-nav-open') ? close() : open());
  backdrop.addEventListener('click', () => close(true));
  sidebar.addEventListener('click', e => {
    if (e.target.closest('.panel-nav-link') && matchMedia('(max-width: 1024px)').matches) close();
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && layout.classList.contains('is-nav-open')) close(true);
  });
  matchMedia('(min-width: 1025px)').addEventListener?.('change', e => { if (e.matches) close(); });
}
