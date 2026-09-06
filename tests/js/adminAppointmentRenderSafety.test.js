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

test('guard patches the legacy raw doctor-name sink before async appointment rendering settles', async () => {
    const dashboard = await source('wwwroot/assets/js/managers/admin/adminDashboard.js');
    const guard = await source('wwwroot/assets/js/managers/admin/adminAppointmentRenderGuard.js');

    assert.match(
        dashboard,
        /_doctor\(id\)\s*\{[^}]*DoctorsDictionary\?\.\[id\]\)\?\.fullName\s*\?\?\s*'—'/s
    );
    assert.match(guard, /document\.addEventListener\('DOMContentLoaded',\s*install,\s*\{\s*once:\s*true\s*\}\)/);
});
