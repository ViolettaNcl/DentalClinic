import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('public appointment form localizes feedback for all supported languages', async () => {
    const text = await source('wwwroot/assets/js/managers/public/appointmentForm.js');

    assert.match(text, /import\s+\{\s*getLang\s*\}\s+from\s+'\.\.\/\.\.\/core\/i18n\.js'/);
    assert.match(text, /const\s+PUBLIC_APPOINTMENT_MESSAGES\s*=\s*Object\.freeze/);

    for (const lang of ['ru', 'en', 'fr', 'el', 'ar']) {
        assert.match(text, new RegExp(`\\b${lang}:\\s*Object\\.freeze\\(\\{`));
    }

    assert.match(text, /showError\(appointmentMessage\('phoneRequired'\)\)/);
    assert.match(text, /showSuccess\(appointmentMessage\('success'\)\)/);
    assert.match(text, /showError\(appointmentMessage\('submitError'\)\)/);
});

test('public appointment form does not expose raw server error text in public toast', async () => {
    const text = await source('wwwroot/assets/js/managers/public/appointmentForm.js');

    assert.doesNotMatch(text, /showError\(err\.message\s*\|\|/);
    assert.match(text, /console\.error\('Appointment request submission failed:',\s*err\)/);
});
