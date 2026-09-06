import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('admin appointment renderer escapes stored doctor names before innerHTML', async () => {
    const guard = await source('wwwroot/assets/js/managers/admin/adminAppointmentRenderGuard.js');

    assert.match(guard, /window\.DoctorsDictionary\?\.\[id\]\?\.fullName/);
    assert.match(guard, /escapeHtml\(fullName\)/);
    assert.match(guard, /manager\._doctor\s*=\s*function\s+safeDoctorName/);
});

test('stored doctor-name guard is installed during admin session bootstrap', async () => {
    const logoutGuard = await source('wwwroot/assets/js/managers/admin/adminLogoutGuard.js');

    assert.match(logoutGuard, /import\s+\{\s*installAdminAppointmentRenderGuard\s*\}\s+from\s+'\.\/adminAppointmentRenderGuard\.js'/);
    assert.match(logoutGuard, /installAdminAppointmentRenderGuard\(\)/);
});

test('guard intercepts manager publication instead of relying on DOMContentLoaded listener ordering', async () => {
    const dashboard = await source('wwwroot/assets/js/managers/admin/adminDashboard.js');
    const guard = await source('wwwroot/assets/js/managers/admin/adminAppointmentRenderGuard.js');

    // The dashboard yields before creating/publishing the request manager. This is why
    // a second DOMContentLoaded listener is not a safe synchronization point.
    assert.match(
        dashboard,
        /await\s+loadDoctors\(\);[\s\S]*requests\.init\(\);\s*window\.AdminRequestsManagerInstance\s*=\s*requests;/
    );

    assert.match(guard, /Object\.defineProperty\(window,\s*property,/);
    assert.match(guard, /set\(value\)\s*\{[\s\S]*harden\(value\);[\s\S]*Object\.defineProperty\(window,\s*property,/);
    assert.doesNotMatch(guard, /document\.addEventListener\('DOMContentLoaded'/);
});

test('legacy raw doctor-name sink is still covered by the publication guard', async () => {
    const dashboard = await source('wwwroot/assets/js/managers/admin/adminDashboard.js');

    assert.match(
        dashboard,
        /_doctor\(id\)\s*\{[^}]*DoctorsDictionary\?\.\[id\]\)\?\.fullName\s*\?\?\s*'—'/s
    );
});
