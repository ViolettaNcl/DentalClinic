import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const chatController = await readFile(
    new URL('../../Controllers/ChatController.cs', import.meta.url),
    'utf8');
const appointmentController = await readFile(
    new URL('../../Controllers/AppointmentRequestController.cs', import.meta.url),
    'utf8');

test('paid chat provider I/O observes request cancellation and disposes responses', () => {
    assert.doesNotMatch(chatController, /ReadAsStringAsync\(\)/);
    assert.doesNotMatch(chatController, /ReadAsStreamAsync\(\)/);
    assert.doesNotMatch(chatController, /ReadLineAsync\(\)/);
    assert.doesNotMatch(chatController, /FlushAsync\(\)/);

    assert.match(
        chatController,
        /using var response = await _http\.PostAsync\([\s\S]*?HttpContext\.RequestAborted\);/);
    assert.equal(
        (chatController.match(/catch \(OperationCanceledException\) when \(HttpContext\.RequestAborted\.IsCancellationRequested\)/g) || []).length,
        2);
    assert.equal(
        (chatController.match(/using var upstreamResponseLease = upstreamResp;/g) || []).length,
        2);
});

test('appointment creation does not translate request cancellation into HTTP 500', () => {
    const createStart = appointmentController.indexOf('public async Task<IActionResult> Create(');
    const createEnd = appointmentController.indexOf('// Редактировать заявку', createStart);
    const createAction = appointmentController.slice(createStart, createEnd);

    const cancellationCatch = createAction.indexOf('catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)');
    const genericCatch = createAction.indexOf('catch (Exception ex)');

    assert.ok(createStart >= 0 && createEnd > createStart);
    assert.ok(cancellationCatch >= 0 && cancellationCatch < genericCatch);
    assert.match(createAction.slice(cancellationCatch, genericCatch), /throw;/);
});
