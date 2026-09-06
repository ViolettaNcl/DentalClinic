import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const uiUrl = new URL('../../wwwroot/assets/js/services/ui.js', import.meta.url);

async function source() {
    return readFile(uiUrl, 'utf8');
}

test('toast session payload cannot inject type, title, icon or duration into markup', async () => {
    const text = await source();

    assert.match(text, /const safeType = normalizeToastType\(type\)/);
    assert.match(text, /duration = normalizeToastDuration\(duration\)/);
    assert.match(text, /toast\.querySelector\('\.toast-icon'\)\.textContent = icon/);
    assert.match(text, /toast\.querySelector\('\.toast-title'\)\.textContent = title/);
    assert.match(text, /toast\.querySelector\('\.toast-progress'\)\.style\.animationDuration = `\$\{duration\}ms`/);

    assert.doesNotMatch(text, /toast-icon">\$\{icon\}/);
    assert.doesNotMatch(text, /toast-title">\$\{title\}/);
    assert.doesNotMatch(text, /toast-\$\{type\}/);
    assert.doesNotMatch(text, /animation-duration:\$\{duration\}/);
});

test('confirmation icon is escaped before entering innerHTML', async () => {
    const text = await source();
    assert.match(text, /panel-confirm-icon">\$\{escapeHtml\(icon\)\}/);
});
