import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('chat statistics keep exact aggregates in the database and bound topic text sampling', async () => {
    const controller = await source('Controllers/ChatController.cs');

    assert.match(controller, /private const int MaxStatsTopicMessages = 5000;/);
    assert.match(controller, /GetStats\([\s\S]*CancellationToken cancellationToken = default\)/);
    assert.match(controller, /var totalMessages = await userQuery\.CountAsync\(cancellationToken\);/);
    assert.match(controller, /GroupBy\(m => m\.CreatedAt\.Date\)[\s\S]*ToListAsync\(cancellationToken\);/);
    assert.match(controller, /Select\(m => m\.Text\)[\s\S]*Take\(MaxStatsTopicMessages\)[\s\S]*ToListAsync\(cancellationToken\);/);
    assert.match(controller, /topicsSampled = totalMessages > topicMessages\.Count/);
    assert.doesNotMatch(controller, /var userMessages = await _db\.ChatMessageLogs[\s\S]{0,250}ToListAsync\(\);/);
});

test('recent chat transcript loading has a hard upper bound', async () => {
    const controller = await source('Controllers/ChatController.cs');

    assert.match(controller, /private const int MaxRecentSessionMessages = 10000;/);
    assert.match(controller, /Where\(m => recentSessionIds\.Contains\(m\.SessionId\)\)[\s\S]*Take\(MaxRecentSessionMessages\)[\s\S]*ToListAsync\(cancellationToken\);/);
    assert.match(controller, /X-Chat-Analytics-Truncated/);
});
