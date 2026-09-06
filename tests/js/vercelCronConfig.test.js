import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const config = JSON.parse(fs.readFileSync(new URL('../../vercel.json', import.meta.url), 'utf8'));
const crons = Array.isArray(config.crons) ? config.crons : [];

function cronByPath(path) {
    return crons.find(cron => cron?.path === path);
}

test('Vercel cron configuration covers maintenance jobs that do not run as hosted services', () => {
    const required = [
        '/api/maintenance/reminders',
        '/api/maintenance/follow-ups',
        '/api/maintenance/cleanup',
        '/api/maintenance/chat-retention',
    ];

    for (const path of required) {
        const cron = cronByPath(path);
        assert.ok(cron, `missing Vercel cron for ${path}`);
        assert.match(String(cron.schedule || ''), /^\S+(\s+\S+){4}$/, `${path} must have a five-field cron schedule`);
    }
});

test('maintenance cron paths are unique', () => {
    const paths = crons.map(cron => cron?.path).filter(Boolean);
    assert.equal(new Set(paths).size, paths.length);
});
