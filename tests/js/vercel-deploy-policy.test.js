import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

test('production container uses the app registry after the legacy web registry reached quota', async () => {
    const raw = await readFile(new URL('vercel.json', root), 'utf8');
    const config = JSON.parse(raw);

    assert.deepEqual(config.git?.deploymentEnabled, {
        '*': false,
        main: true
    });

    assert.equal(config.services?.app?.entrypoint, 'Dockerfile.vercel');
    assert.equal(config.services?.web, undefined);
    assert.equal(config.rewrites?.[0]?.destination?.service, 'app');
});
