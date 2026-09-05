import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildAdminAnalyticsViewModel,
    formatDayLabel,
} from '../../wwwroot/assets/js/managers/admin/adminAnalyticsSummary.js';

test('formats ISO dates for the 30-day chart', () => {
    assert.equal(formatDayLabel('2026-09-05'), '05.09');
    assert.equal(formatDayLabel('2026-01-02T10:30:00'), '02.01');
    assert.equal(formatDayLabel('bad-date'), 'bad-date');
});

test('maps the server analytics contract to dashboard cards and charts', () => {
    const view = buildAdminAnalyticsViewModel({
        totalRequests: 20,
        thisMonthRequests: 8,
        confirmedOrCompletedRate: 55.5,
        statuses: {
            pending: 3,
            confirmed: 4,
            completed: 7,
            cancelled: 5,
            unknown: 1,
        },
        sources: {
            registered: 7,
            guest: 8,
            denta: 5,
        },
        byDay: [
            { date: '2026-09-04', count: 2 },
            { date: '2026-09-05', count: 4 },
        ],
        byDoctor: [
            { doctorId: 2, doctorName: 'Dr. A', count: 6 },
            { doctorId: 4, doctorName: 'Dr. B', count: 3 },
        ],
        generatedAt: '2026-09-05T20:00:00',
    });

    assert.deepEqual(view.cards, {
        total: 20,
        month: 8,
        confirmedRate: '55,5%',
        pending: 3,
    });
    assert.deepEqual(view.statusTotals, {
        pending: 3,
        confirmed: 4,
        completed: 7,
        cancelled: 5,
        unknown: 1,
    });
    assert.deepEqual(view.sourceChart.data, [7, 8, 5]);
    assert.equal(view.sourceChart.data.reduce((a, b) => a + b, 0), 20);
    assert.deepEqual(view.dayChart, {
        labels: ['04.09', '05.09'],
        data: [2, 4],
    });
    assert.deepEqual(view.doctorChart, {
        labels: ['Dr. A', 'Dr. B'],
        data: [6, 3],
    });
});

test('uses stable zero-value fallbacks for an empty response', () => {
    const view = buildAdminAnalyticsViewModel({});

    assert.deepEqual(view.cards, {
        total: 0,
        month: 0,
        confirmedRate: '0%',
        pending: 0,
    });
    assert.deepEqual(view.sourceChart.data, [0, 0, 0]);
    assert.deepEqual(view.dayChart, { labels: [], data: [] });
    assert.deepEqual(view.doctorChart, { labels: [], data: [] });
});

test('keeps chart arrays numeric when API values are serialized as strings', () => {
    const view = buildAdminAnalyticsViewModel({
        totalRequests: '2',
        thisMonthRequests: '1',
        confirmedOrCompletedRate: '50',
        statuses: { pending: '1' },
        sources: { registered: '1', guest: '0', denta: '1' },
        byDay: [{ date: '2026-09-05', count: '2' }],
        byDoctor: [{ doctorId: 9, doctorName: '', count: '2' }],
    });

    assert.equal(view.cards.total, 2);
    assert.equal(view.cards.month, 1);
    assert.equal(view.cards.pending, 1);
    assert.equal(view.cards.confirmedRate, '50%');
    assert.deepEqual(view.sourceChart.data, [1, 0, 1]);
    assert.deepEqual(view.dayChart.data, [2]);
    assert.deepEqual(view.doctorChart.data, [2]);
    assert.deepEqual(view.doctorChart.labels, ['Врач #9']);
});
