import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);
const read = path => readFile(new URL(path, root), 'utf8');

test('Render blueprint uses the port-aware container image and the free plan', async () => {
    const blueprint = await read('render.yaml');
    assert.match(blueprint, /runtime:\s*docker/);
    assert.match(blueprint, /plan:\s*free/);
    assert.match(blueprint, /dockerfilePath:\s*\.\/Dockerfile\.vercel/);
    assert.match(blueprint, /healthCheckPath:\s*\/health/);

    const dockerfile = await read('Dockerfile.vercel');
    assert.match(dockerfile, /\$\{PORT:-8080\}/);
});

test('Render blueprint never contains secret values', async () => {
    const blueprint = await read('render.yaml');
    for (const key of [
        'ConnectionStrings__DefaultConnection',
        'Jwt__Key',
        'Gemini__ApiKey',
        'ElevenLabs__ApiKey'
    ]) {
        const block = blueprint.slice(blueprint.indexOf(`key: ${key}`));
        const next = block.split('\n').slice(1, 2).join('');
        assert.match(next, /sync:\s*false/, `${key} must be provided in the dashboard, not committed`);
    }
    assert.doesNotMatch(blueprint, /password=|pwd=|AIza/i);
});

test('forwarded headers are trusted on Render as well as Vercel', async () => {
    const program = await read('Program.cs');
    assert.match(program, /GetEnvironmentVariable\(\s*"RENDER"\s*\)/);
    assert.match(program, /var trustForwardedHeaders = isVercel\s*\|\|\s*isRender/);
    assert.match(program, /if \(trustForwardedHeaders\)\s*\{\s*app\.UseForwardedHeaders\(\);/);
});
