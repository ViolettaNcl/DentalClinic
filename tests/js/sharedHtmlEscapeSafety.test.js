import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { escapeHtmlAttribute } from '../../wwwroot/assets/js/services/htmlAttributeSafety.js';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('shared attribute-safe escape neutralizes quote breakout payloads', () => {
    const payload = '" autofocus onfocus="alert(1)\' onmouseover=\'alert(2)<svg>';
    const escaped = escapeHtmlAttribute(payload);

    assert.match(escaped, /&quot;/);
    assert.match(escaped, /&#39;/);
    assert.doesNotMatch(escaped, /["']/);
    assert.doesNotMatch(escaped, /</);
});

test('shared ui escape delegates to attribute-safe escaping', async () => {
    const ui = await source('wwwroot/assets/js/services/ui.js');

    assert.match(ui, /import\s+\{\s*escapeHtmlAttribute\s*\}\s+from\s+'\.\/htmlAttributeSafety\.js'/);
    assert.match(ui, /export\s+function\s+escapeHtml\(str\)\s*\{\s*return\s+escapeHtmlAttribute\(str\);\s*\}/s);
});

test('patient dashboard quoted translation attributes use the hardened shared escape', async () => {
    const dashboard = await source('wwwroot/assets/js/managers/patient/patientDashboard.js');

    assert.match(dashboard, /import\s+\{[^}]*escapeHtml[^}]*\}\s+from\s+'\.\.\/\.\.\/services\/ui\.js'/s);
    assert.match(dashboard, /data-translate-name="\$\{escapeHtml\(a\.doctorName\)\}"/);
    assert.match(dashboard, /data-translate-text="\$\{escapeHtml\(a\.comment\)\}"/);
});
