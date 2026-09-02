(() => {
  const TEETH = [
    18, 17, 16, 15, 14, 13, 12, 11,
    21, 22, 23, 24, 25, 26, 27, 28,
    48, 47, 46, 45, 44, 43, 42, 41,
    31, 32, 33, 34, 35, 36, 37, 38
  ];

  function renderToothChart(root) {
    if (!root || root.dataset.ready === '1') return;

    root.dataset.ready = '1';
    root.classList.add('tooth-chart');
    root.setAttribute('role', 'group');
    if (!root.getAttribute('aria-label')) root.setAttribute('aria-label', 'Dental chart');

    for (const number of TEETH) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'tooth-chart__tooth';
      button.dataset.tooth = String(number);
      button.setAttribute('aria-pressed', 'false');
      button.setAttribute('aria-label', `Tooth ${number}`);
      button.innerHTML = `<span aria-hidden="true">🦷</span><small>${number}</small>`;
      button.addEventListener('click', () => {
        const selected = button.getAttribute('aria-pressed') !== 'true';
        button.setAttribute('aria-pressed', String(selected));
        root.dispatchEvent(new CustomEvent('toothchange', {
          bubbles: true,
          detail: { tooth: number, selected }
        }));
      });
      root.appendChild(button);
    }
  }

  function initToothCharts() {
    document.querySelectorAll('[data-tooth-chart]').forEach(renderToothChart);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initToothCharts);
  } else {
    initToothCharts();
  }

  window.DentalToothChart = Object.freeze({ TEETH, renderToothChart, initToothCharts });
})();
