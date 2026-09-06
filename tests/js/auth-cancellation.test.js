import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('auth database operations propagate request cancellation', async () => {
    const controller = await source('Controllers/AuthController.cs');

    for (const action of ['Register', 'Login', 'AdminLogin', 'Logout', 'GetSession', 'GetProfile', 'GetAdminProfile', 'UpdateProfile', 'ChangePassword']) {
        assert.match(controller, new RegExp(`${action}\\([\\s\\S]*?CancellationToken cancellationToken`));
    }

    assert.doesNotMatch(controller, /SaveChangesAsync\(\)/);
    assert.doesNotMatch(controller, /FirstOrDefaultAsync\(\)/);
    assert.match(controller, /NotifyAsync\([\s\S]*cancellationToken\);/);
});

test('read-only login and profile lookups use no-tracking queries', async () => {
    const controller = await source('Controllers/AuthController.cs');

    assert.match(controller, /_db\.Patients[\s\S]*AsNoTracking\(\)[\s\S]*FirstOrDefaultAsync\(p => p\.Email == email, cancellationToken\)/);
    assert.match(controller, /_db\.Admins[\s\S]*AsNoTracking\(\)[\s\S]*FirstOrDefaultAsync\(a => a\.Email == email, cancellationToken\)/);
    assert.match(controller, /GetProfile\([\s\S]*AsNoTracking\(\)[\s\S]*FirstOrDefaultAsync\(p => p\.Id == patientId, cancellationToken\)/);
});
