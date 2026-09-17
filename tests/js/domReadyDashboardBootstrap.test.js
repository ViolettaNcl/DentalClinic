import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { runWhenDomReady } from '../../wwwroot/assets/js/core/domReady.js';

test('runs an initializer on the next microtask when a module loads after DOMContentLoaded', async () => {
    let calls = 0;
    const documentRef = {
        readyState: 'complete',
        addEventListener() {
            assert.fail('a completed document must not wait for another DOMContentLoaded event');
        }
    };

    runWhenDomReady(() => { calls += 1; }, documentRef);

    assert.equal(calls, 0);
    await Promise.resolve();
    assert.equal(calls, 1);
});

test('waits exactly once while the document is still loading', () => {
    let listener;
    let options;
    let calls = 0;
    const documentRef = {
        readyState: 'loading',
        addEventListener(name, callback, suppliedOptions) {
            assert.equal(name, 'DOMContentLoaded');
            listener = callback;
            options = suppliedOptions;
        }
    };

    runWhenDomReady(() => { calls += 1; }, documentRef);

    assert.equal(calls, 0);
    assert.deepEqual(options, { once: true });
    listener();
    assert.equal(calls, 1);
});

test('admin, patient and doctor dashboards use the late-import-safe initializer', async () => {
    const files = [
        '../../wwwroot/assets/js/managers/admin/adminDashboard.js',
        '../../wwwroot/assets/js/managers/admin/reviewModeration.js',
        '../../wwwroot/assets/js/managers/patient/patientDashboard.js',
        '../../wwwroot/assets/js/managers/public/myReviews.js',
        '../../wwwroot/assets/js/managers/admin/doctorsManager.js'
    ];

    for (const file of files) {
        const source = await readFile(new URL(file, import.meta.url), 'utf8');
        assert.match(source, /runWhenDomReady\s*\(/, file);
    }
});

test('admin initializes navigation first, then core managers after session bootstrap without waiting for a slow doctor request', async () => {
    const { runInNewContext } = await import('node:vm');
    const source = await readFile(new URL('../../wwwroot/assets/js/managers/admin/adminDashboard.js', import.meta.url), 'utf8');
    const bootstrap = source.slice(source.indexOf('runWhenDomReady(async () => {'), source.indexOf('/* =====================================================\n   ЭКСПОРТ:'));

    for (const readyState of ['loading', 'complete']) {
        let releaseDoctors;
        const doctors = new Promise(resolve => { releaseDoctors = resolve; });
        let listener;
        let initialized;
        const calls = [];
        const window = {};
        const document = {
            readyState,
            getElementById: () => null,
            addEventListener: (_, callback) => { listener = callback; }
        };
        runInNewContext(bootstrap, {
            window, document,
            runWhenDomReady: callback => runWhenDomReady(() => { initialized = callback(); }, document),
            checkAdminAccess: () => true,
            initNav() { calls.push('nav-init'); },
            bootstrapAdminSession: async () => ({ role: 'admin' }),
            loadDoctors: () => doctors,
            DoctorCalendarManager: class { init() { calls.push('calendar-init'); assert.equal(this.enhanced, true); } },
            installDoctorCalendarAvailability() { window.DoctorCalendarManagerInstance.enhanced = true; },
            installAdminAnalyticsSummary() {
                assert.ok(window.AnalyticsManagerInstance);
                assert.ok(window.AdminRequestsManagerInstance);
            },
            AnalyticsManager: class { init() { calls.push('analytics-init'); } },
            AdminRequestsManager: class { init() { calls.push('requests-init'); } },
            initPhoneForm() {}, initAdminProfileLazy() {}, initExportButtons() {}
        });
        if (readyState === 'loading') listener();
        else await Promise.resolve();

        await initialized;
        assert.ok(window.DoctorCalendarManagerInstance);
        assert.ok(window.AnalyticsManagerInstance);
        assert.ok(window.AdminRequestsManagerInstance);
        assert.deepEqual(calls, ['nav-init', 'calendar-init', 'analytics-init', 'requests-init']);
        releaseDoctors();
        await Promise.resolve();
        assert.deepEqual(calls, ['nav-init', 'calendar-init', 'analytics-init', 'requests-init']);
    }
});

test('admin and patient session checks are fire-and-forget so remote SQL cannot block DOMContentLoaded', async () => {
    const adminGuard = await readFile(new URL('../../wwwroot/assets/js/managers/admin/adminLogoutGuard.js', import.meta.url), 'utf8');
    const patientEntry = await readFile(new URL('../../wwwroot/assets/js/managers/patient/patientDashboardEntry.js', import.meta.url), 'utf8');
    const adminDashboard = await readFile(new URL('../../wwwroot/assets/js/managers/admin/adminDashboard.js', import.meta.url), 'utf8');

    assert.match(adminGuard, /void bootstrapAdminSession\(\);/);
    assert.match(patientEntry, /void bootstrapPatientDashboard\(\);/);
    assert.match(patientEntry, /runWhenDomReady\(\(\) => \{[\s\S]*TabManager/);

    const initBlock = adminDashboard.slice(adminDashboard.indexOf('runWhenDomReady(async () => {'));
    assert.ok(initBlock.indexOf('initNav();') < initBlock.indexOf('await bootstrapAdminSession();'));
});
