import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const dentaAiService = await readFile(
    new URL('../../Services/DentaAiService.cs', import.meta.url),
    'utf8');
const chatController = await readFile(
    new URL('../../Controllers/ChatController.cs', import.meta.url),
    'utf8');
const appointmentController = await readFile(
    new URL('../../Controllers/AppointmentRequestController.cs', import.meta.url),
    'utf8');

test('paid chat provider I/O observes request cancellation and disposes responses', () => {
    assert.match(dentaAiService, /client\.SendAsync\([\s\S]*?cancellationToken\)/);
    assert.match(dentaAiService, /ReadAsStringAsync\(cancellationToken\)/);
    assert.match(dentaAiService, /using \(response\)/);
    assert.match(dentaAiService, /catch \(OperationCanceledException\) when \(cancellationToken\.IsCancellationRequested\)/);

    assert.match(chatController, /Response\.WriteAsync\([\s\S]*?HttpContext\.RequestAborted\)/);
    assert.match(chatController, /Response\.Body\.FlushAsync\(HttpContext\.RequestAborted\)/);
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
