import { apiFetch } from '../../services/apiClient.js';

export function buildAdminAnalyticsViewModel(summary = {}) {
    const statuses = summary.statuses || {};
    const sources = summary.sources || {};
    const byDay = Array.isArray(summary.byDay) ? summary.byDay : [];
    const byDoctor = Array.isArray(summary.byDoctor) ? summary.byDoctor : [];

    const number = (value) => Number.isFinite(Number(value)) ? Number(value) : 0;
    const total = number(summary.totalRequests);
    const rate = number(summary.confirmedOrCompletedRate);

    return {
        cards: {
            total,
            month: number(summary.thisMonthRequests),
            confirmedRate: `${rate.toLocaleString('ru-RU', { maximumFractionDigits: 1 })}%`,
            pending: number(statuses.pending),
        },
        statusTotals: {
            pending: number(statuses.pending),
            confirmed: number(statuses.confirmed),
            completed: number(statuses.completed),
            cancelled: number(statuses.cancelled),
            unknown: number(statuses.unknown),
        },
        sourceChart: {
            labels: ['Пользователи', 'Гости', 'Через Денту'],
            data: [number(sources.registered), number(sources.guest), number(sources.denta)],
        },
        dayChart: {
            labels: byDay.map(item => formatDayLabel(item.date)),
            data: byDay.map(item => number(item.count)),
        },
        doctorChart: {
            labels: byDoctor.map(item => String(item.doctorName || `Врач #${item.doctorId ?? '—'}`)),
            data: byDoctor.map(item => number(item.count)),
        },
        generatedAt: summary.generatedAt || null,
    };
}

export function formatDayLabel(value) {
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value || ''));
    return match ? `${match[3]}.${match[2]}` : String(value || '');
}

function renderCards(viewModel) {
    const pairs = [
        ['an-total', viewModel.cards.total],
        ['an-month', viewModel.cards.month],
        ['an-confirmed-rate', viewModel.cards.confirmedRate],
        ['an-pending', viewModel.cards.pending],
    ];

    pairs.forEach(([id, value]) => {
        const element = document.getElementById(id);
        if (element) element.textContent = String(value);
    });
}

function renderCharts(viewModel, analytics) {
    if (typeof Chart === 'undefined') return false;

    const byDay = document.getElementById('chart-by-day');
    const source = document.getElementById('chart-source');
    const doctors = document.getElementById('chart-doctors');

    analytics.charts ||= {};

    if (byDay) {
        analytics.charts.byDay?.destroy?.();
        analytics.charts.byDay = new Chart(byDay, {
            type: 'line',
            data: {
                labels: viewModel.dayChart.labels,
                datasets: [{
                    label: 'Заявки',
                    data: viewModel.dayChart.data,
                    borderColor: '#13b39b',
                    backgroundColor: 'rgba(19,179,155,0.14)',
                    fill: true,
                    tension: 0.35,
                    pointRadius: 2,
                    pointHoverRadius: 5,
                    pointBackgroundColor: '#0a8a77',
                    borderWidth: 2,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { grid: { display: false }, ticks: { maxTicksLimit: 10, font: { size: 11 } } },
                    y: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } }, grid: { color: '#eef3f1' } },
                },
            },
        });
    }

    if (source) {
        analytics.charts.source?.destroy?.();
        analytics.charts.source = new Chart(source, {
            type: 'doughnut',
            data: {
                labels: viewModel.sourceChart.labels,
                datasets: [{
                    data: viewModel.sourceChart.data,
                    backgroundColor: ['#13b39b', '#5fd9c4', '#0a6fad'],
                    borderWidth: 2,
                    borderColor: '#ffffff',
                    hoverOffset: 6,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '62%',
                plugins: {
                    legend: { position: 'bottom', labels: { font: { size: 12 }, padding: 12, boxWidth: 12 } },
                },
            },
        });
    }

    if (doctors) {
        analytics.charts.doctors?.destroy?.();
        const wrap = doctors.closest('.analytics-chart-wrap');
        const empty = wrap?.querySelector('.analytics-chart-empty[data-server-summary]');
        empty?.remove();
        doctors.hidden = false;

        if (!viewModel.doctorChart.data.length) {
            doctors.hidden = true;
            if (wrap) {
                const message = document.createElement('div');
                message.className = 'analytics-chart-empty';
                message.dataset.serverSummary = 'true';
                message.textContent = 'Нет данных по врачам';
                wrap.appendChild(message);
            }
            analytics.charts.doctors = null;
        } else {
            analytics.charts.doctors = new Chart(doctors, {
                type: 'bar',
                data: {
                    labels: viewModel.doctorChart.labels,
                    datasets: [{
                        label: 'Записей',
                        data: viewModel.doctorChart.data,
                        backgroundColor: '#13b39b',
                        hoverBackgroundColor: '#0a8a77',
                        borderRadius: 6,
                        maxBarThickness: 28,
                    }],
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { beginAtZero: true, ticks: { precision: 0, font: { size: 11 } }, grid: { color: '#eef3f1' } },
                        y: { grid: { display: false }, ticks: { font: { size: 11 } } },
                    },
                },
            });
        }
    }

    return true;
}

export function installAdminAnalyticsSummary() {
    if (typeof document === 'undefined' || typeof window === 'undefined') return;

    document.addEventListener('DOMContentLoaded', () => {
        const analytics = window.AnalyticsManagerInstance;
        if (!analytics) return;

        // Appointment tables still load their full rows for CRM operations, but the
        // analytics cards/charts now use the tested server-side aggregate endpoint.
        // Prevent the legacy client-side reducer from overwriting those values.
        analytics.setData = () => {};

        let lastSummary = null;
        let chartRetry = null;

        const render = (summary) => {
            lastSummary = summary;
            const viewModel = buildAdminAnalyticsViewModel(summary);
            renderCards(viewModel);

            if (!renderCharts(viewModel, analytics)) {
                clearTimeout(chartRetry);
                chartRetry = setTimeout(() => {
                    if (lastSummary) renderCharts(buildAdminAnalyticsViewModel(lastSummary), analytics);
                }, 150);
            }
        };

        const loadSummary = async ({ silent = false } = {}) => {
            try {
                const summary = await apiFetch('/adminstats/summary');
                render(summary);
                return summary;
            } catch (error) {
                console.error('Admin analytics summary error:', error);
                if (!silent) {
                    const total = document.getElementById('an-total');
                    if (total && total.textContent === '—') total.textContent = 'Ошибка';
                }
                return null;
            }
        };

        window.refreshAdminAnalyticsSummary = loadSummary;

        const requests = window.AdminRequestsManagerInstance;
        if (requests && !requests.__serverAnalyticsWrapped) {
            const originalLoadAll = requests.loadAll.bind(requests);
            requests.loadAll = async (...args) => {
                try {
                    return await originalLoadAll(...args);
                } finally {
                    await loadSummary({ silent: true });
                }
            };
            requests.__serverAnalyticsWrapped = true;
        }

        document.querySelector('.panel-nav-link[data-section="analytics"]')
            ?.addEventListener('click', () => loadSummary({ silent: true }));

        loadSummary();
    });
}
