import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import {
    PASSWORD_MIN_LENGTH,
    isStrongPassword,
    passwordRequirementsMessage
} from '../../wwwroot/assets/js/core/passwordPolicy.js';

const root = new URL('../../', import.meta.url);
const source = path => readFile(new URL(path, root), 'utf8');

test('browser password policy mirrors the server strength requirements', () => {
    assert.equal(PASSWORD_MIN_LENGTH, 8);
    assert.equal(isStrongPassword('Password1!'), true);
    assert.equal(isStrongPassword('ПарольА1!'), true);
    assert.equal(isStrongPassword('password1!'), false);
    assert.equal(isStrongPassword('PASSWORD1!'), false);
    assert.equal(isStrongPassword('Password!!'), false);
    assert.equal(isStrongPassword('Password11'), false);
    assert.equal(isStrongPassword('Aa1!'), false);
});

test('password requirement guidance is available for every supported UI language', () => {
    for (const lang of ['ru', 'en', 'fr', 'el', 'ar']) {
        const message = passwordRequirementsMessage(lang);
        assert.ok(message.length > 20, `missing password guidance for ${lang}`);
        assert.match(message, /8/);
    }
});

test('signup validates new credentials without applying strength checks to login', async () => {
    const text = await source('wwwroot/assets/js/managers/auth/authManager.js');

    assert.match(text, /type === 'signup' && !isStrongPassword\(data\.password\)/);
    assert.doesNotMatch(text, /type === 'login' && !isStrongPassword/);
    assert.match(text, /password\.minLength = PASSWORD_MIN_LENGTH/);
});

test('patient password guard is installed before legacy dashboard handlers', async () => {
    const entry = await source('wwwroot/assets/js/managers/patient/patientDashboardEntry.js');
    const guard = await source('wwwroot/assets/js/managers/patient/patientPasswordPolicyGuard.js');

    const installIndex = entry.indexOf('installPatientPasswordPolicyGuard();');
    const dashboardImportIndex = entry.indexOf("await import('./patientDashboard.js')");
    assert.ok(installIndex >= 0 && installIndex < dashboardImportIndex);
    assert.match(guard, /event\.stopImmediatePropagation\(\)/);
    assert.match(guard, /isStrongPassword\(password\.value\)/);
    assert.match(guard, /password\.minLength = PASSWORD_MIN_LENGTH/);
});

test('signup HTML advertises the current minimum and password autocomplete semantics', async () => {
    const html = await source('wwwroot/pages/header.html');

    assert.match(html, /id="signupForm"[\s\S]*type="password"[^>]*minlength="8"[^>]*autocomplete="new-password"/);
    assert.match(html, /id="loginForm"[\s\S]*type="password"[^>]*autocomplete="current-password"/);
});
