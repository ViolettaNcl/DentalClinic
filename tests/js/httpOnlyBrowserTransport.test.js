import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('realtime never exposes JWT in JavaScript or SignalR query string', async () => {
    const text = await source('wwwroot/assets/js/services/realtime.js');

    assert.doesNotMatch(text, /authToken/);
    assert.doesNotMatch(text, /access_token/);
    assert.match(text, /\.withUrl\('\/hubs\/notifications'\)/);
    assert.match(text, /sessionStorage\.getItem\('userRole'\)/);
});

test('avatar mutations rely on same-origin HttpOnly cookie', async () => {
    const text = await source('wwwroot/assets/js/services/avatarService.js');

    assert.doesNotMatch(text, /authToken/);
    assert.doesNotMatch(text, /Authorization/);
    assert.match(text, /method:\s*'POST',[\s\S]*?credentials:\s*'same-origin'/);
    assert.match(text, /method:\s*'DELETE',[\s\S]*?credentials:\s*'same-origin'/);
});
