import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('relational schema migrations are not skipped on Vercel startup', async () => {
    const program = await source('Program.cs');
    const startupStart = program.indexOf('using (var scope = app.Services.CreateScope())');
    const startupEnd = program.indexOf('app.Run();', startupStart);

    assert.notEqual(startupStart, -1);
    assert.notEqual(startupEnd, -1);

    const startup = program.slice(startupStart, startupEnd);
    assert.match(startup, /if\s*\(db\.Database\.IsRelational\(\)\)\s*await\s+db\.Database\.MigrateAsync\(\)/);
    assert.doesNotMatch(startup, /!isVercel\s*&&\s*db\.Database\.IsRelational\(\)/);

    // Seeding must remain after migrations so seed queries never run against an
    // older schema during a fresh container/cold start.
    assert.ok(startup.indexOf('MigrateAsync') < startup.indexOf('DbSeeder.SeedAsync'));
});
