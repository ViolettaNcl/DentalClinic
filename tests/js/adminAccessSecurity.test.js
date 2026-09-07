import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const managerUrl = new URL('../../wwwroot/assets/js/managers/admin/adminAccessManager.js', import.meta.url);
const guardUrl = new URL('../../wwwroot/assets/js/managers/admin/adminLogoutGuard.js', import.meta.url);

const managerSource = await readFile(managerUrl, 'utf8');
const guardSource = await readFile(guardUrl, 'utf8');

test('admin access dashboard keeps server-provided account values out of HTML injection sinks', () => {
    assert.match(managerSource, /cell\.textContent = text/);
    assert.match(managerSource, /label\.textContent = `Аккаунт: \$\{admin\.email\}`/);
    assert.doesNotMatch(managerSource, /innerHTML\s*=\s*`[^`]*\$\{admin\.(?:email|id|createdAt)/s);
});

test('admin password management uses masked inputs and revokes self-session UX', () => {
    assert.match(managerSource, /type="password"/);
    assert.match(managerSource, /targetId === currentAdminId/);
    assert.match(managerSource, /sessionStorage\.clear\(\)/);
    assert.match(managerSource, /window\.location\.href = '\/index\.html'/);
});

test('ordinary admins never keep a stale super-admin section selected', () => {
    assert.match(managerSource, /error\?\.status === 401 \|\| error\?\.status === 403/);
    assert.match(managerSource, /sessionStorage\.setItem\(STORAGE_KEY, 'requests'\)/);
});

test('DOM-only access manager is dynamically imported behind the browser guard', () => {
    const browserGuard = /if \(typeof window !== 'undefined' && typeof document !== 'undefined'\) \{[\s\S]*await import\('\.\/adminAccessManager\.js'\)/;
    assert.match(guardSource, browserGuard);
    assert.doesNotMatch(guardSource, /^import ['"]\.\/adminAccessManager\.js['"];?$/m);
});
