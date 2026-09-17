import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);
const source = path => readFile(new URL(path, root), 'utf8');

test('session bootstrap endpoint uses validated JWT claims instead of another remote SQL query', async () => {
    const controller = await source('Controllers/AuthController.cs');
    const start = controller.indexOf('GetSession(CancellationToken cancellationToken)');
    const end = controller.indexOf('[HttpGet("profile")]', start);
    const block = controller.slice(start, end);

    assert.ok(start >= 0 && end > start);
    assert.match(block, /FindFirstValue\(ClaimTypes\.Email\)/);
    assert.match(block, /FindFirstValue\(ClaimTypes\.Name\)/);
    assert.doesNotMatch(block, /_db\.(Patients|Admins)/);
    assert.doesNotMatch(block, /FirstOrDefaultAsync|SingleOrDefaultAsync/);
});

test('database transient failures are handled as retryable 503 responses', async () => {
    const startup = await source('Program.cs');
    const filter = await source('Filters/DatabaseTransientExceptionFilter.cs');
    const api = await source('wwwroot/assets/js/services/apiClient.js');

    assert.match(startup, /DatabaseTransientExceptionFilter/);
    assert.match(filter, /Status503ServiceUnavailable/);
    assert.match(filter, /database_temporarily_unavailable|database_timeout/);
    assert.match(api, /GET_CONCURRENCY_LIMIT\s*=\s*2/);
    assert.match(api, /error\.status === 503/);
    assert.match(api, /DEFAULT_GET_RETRIES\s*=\s*1/);
});

test('doctor and review admin tabs remain retryable after a transient load failure', async () => {
    const doctors = await source('wwwroot/assets/js/managers/admin/doctorsManager.js');
    const reviews = await source('wwwroot/assets/js/managers/admin/reviewModeration.js');

    assert.match(doctors, /if \(success\) this\._loadedOnce = true/);
    assert.match(doctors, /data-retry-doctors/);
    assert.match(reviews, /loadTabOnce\('pending'\)/);
    assert.match(reviews, /if \(success\) this\._loadedTabs\.add\(key\)/);
    assert.match(reviews, /data-retry-review/);
});

test('FAQ and service knowledge tables have responsive card fallbacks', async () => {
    const faq = await source('wwwroot/assets/js/managers/admin/clinicKnowledgeManager.js');
    const services = await source('wwwroot/assets/js/managers/admin/serviceKnowledgeManager.js');

    assert.match(faq, /clinic-knowledge-table/);
    assert.match(faq, /data-label="Содержание"/);
    assert.match(faq, /@media\(max-width:900px\)/);
    assert.match(services, /service-knowledge-table/);
    assert.match(services, /data-label="База Денты"/);
});

test('analytics reviews use one aggregate endpoint instead of three legacy arrays', async () => {
    const dashboard = await source('wwwroot/assets/js/managers/admin/adminDashboard.js');
    const controller = await source('Controllers/ReviewController.cs');

    assert.match(dashboard, /apiFetch\('\/review\/admin\/summary'\)/);
    assert.doesNotMatch(dashboard, /apiFetch\('\/review\/admin\/pending'\)/);
    assert.match(controller, /HttpGet\("admin\/summary"\)/);
});
