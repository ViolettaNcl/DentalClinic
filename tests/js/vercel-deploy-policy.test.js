import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

test('automatic Vercel container deployments are limited to main', async () => {
    const raw = await readFile(new URL('vercel.json', root), 'utf8');
    const config = JSON.parse(raw);

    assert.deepEqual(config.git?.deploymentEnabled, {
        '*': false,
        main: true
    });

    assert.equal(config.services?.web?.entrypoint, 'Dockerfile.vercel');
});
