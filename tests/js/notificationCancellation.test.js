import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('notification reads are bounded, no-tracking and cancellation-aware', async () => {
    const controller = await source('Controllers/Notificationcontroller.cs');

    assert.match(controller, /GetMine\(CancellationToken cancellationToken\)/);
    assert.match(controller, /AsNoTracking\(\)[\s\S]*Take\(30\)[\s\S]*ToListAsync\(cancellationToken\)/);
    assert.match(controller, /GetUnreadCount\(CancellationToken cancellationToken\)/);
    assert.match(controller, /CountAsync\([^;]*cancellationToken\)/s);
});

test('notification mutations propagate request cancellation to EF', async () => {
    const controller = await source('Controllers/Notificationcontroller.cs');

    assert.match(controller, /MarkRead\(int id, CancellationToken cancellationToken\)/);
    assert.match(controller, /MarkAllRead\(CancellationToken cancellationToken\)/);
    assert.match(controller, /Delete\(int id, CancellationToken cancellationToken\)/);
    assert.match(controller, /DeleteAll\(CancellationToken cancellationToken\)/);
    assert.match(controller, /ExecuteUpdateAsync\([^;]*cancellationToken\)/s);
    assert.match(controller, /ExecuteDeleteAsync\(cancellationToken\)/);
});
