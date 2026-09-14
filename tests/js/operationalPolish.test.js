import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('contact page keeps configured address fallback and driving directions', async () => {
    const contact = await source('wwwroot/assets/js/managers/public/contactPage.js');
    const map = await source('wwwroot/assets/js/core/clinicMap.js');

    assert.match(contact, /resolveClinicMapTarget/);
    assert.match(contact, /buildClinicMapEmbedUrl/);
    assert.match(contact, /buildClinicDirectionsUrl/);
    assert.match(map, /travelmode/);
    assert.match(map, /driving/);
});

test('admin and patient async modules use late-import-safe DOM initialization', async () => {
    const paths = [
        'wwwroot/assets/js/managers/admin/adminAnalyticsSummary.js',
        'wwwroot/assets/js/managers/admin/clinicKnowledgeManager.js',
        'wwwroot/assets/js/managers/admin/doctorsManager.js',
        'wwwroot/assets/js/managers/admin/serviceKnowledgeManager.js',
        'wwwroot/assets/js/managers/patient/patientDashboard.js',
        'wwwroot/assets/js/managers/public/myReviews.js'
    ];

    for (const path of paths) {
        const text = await source(path);
        assert.match(text, /runWhenDomReady\s*\(/, `${path} must initialize safely after DOMContentLoaded`);
    }
});

test('admin doctor calendar uses authoritative availability instead of fixed busy-only slots', async () => {
    const doctors = await source('wwwroot/assets/js/managers/admin/doctorsManager.js');
    const availability = await source('wwwroot/assets/js/managers/admin/doctorCalendarAvailability.js');

    assert.match(doctors, /installDoctorCalendarAvailability/);
    assert.match(availability, /\/doctorschedule\/availability\?doctorId=/);
    assert.match(availability, /slot\.isAvailable/);
    assert.match(availability, /blockedReason/);
});

test('example clinic hours stay aligned with scheduling', async () => {
    const settings = JSON.parse(await source('appsettings.Example.json'));
    const hours = settings.Scheduling.WorkingHours;

    for (const day of ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']) {
        assert.equal(hours[day].Open, '09:00');
        assert.equal(hours[day].Close, '20:00');
    }
    assert.equal(hours.Sunday.Closed, true);
    assert.equal(settings.Clinic.Hours, 'Пн-Сб 09:00-20:00; Вс — выходной');
    assert.match(settings.Clinic.Address, /Волгоград/);
});
