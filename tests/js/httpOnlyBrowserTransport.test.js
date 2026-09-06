import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import {
    buildChatAppointmentPayload,
    submitChatAppointment
} from '../../wwwroot/assets/js/core/chatBookingTransport.js';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('realtime never exposes JWT in JavaScript or SignalR query string', async () => {
    const text = await source('wwwroot/assets/js/services/realtime.js');

    assert.doesNotMatch(text, /authToken/);
    assert.doesNotMatch(text, /access_token/);
    assert.match(text, /\.withUrl\('\/hubs\/notifications'\)/);
    assert.match(text, /sessionStorage\.getItem\('userRole'\)/);
});

test('avatar mutations rely on same-origin HttpOnly cookie', async () => {
    const text = await source('wwwroot/assets/js/services/avatarService.js');

    assert.doesNotMatch(text, /authToken/);
    assert.doesNotMatch(text, /Authorization/);
    assert.match(text, /method:\s*'POST',[\s\S]*?credentials:\s*'same-origin'/);
    assert.match(text, /method:\s*'DELETE',[\s\S]*?credentials:\s*'same-origin'/);
});

test('chat appointment transport keeps identity and status server-owned', async () => {
    const payload = buildChatAppointmentPayload(
        { name: 'Анна', phone: '+7 999 123-45-67', comment: 'Болит зуб' },
        {
            parsedDate: '2026-09-08T12:00:00',
            noComment: 'не знаю',
            viaChatTag: '[Заявка через чат]'
        }
    );

    assert.deepEqual(payload, {
        firstName: 'Анна',
        phone: '+7 999 123-45-67',
        appointmentDate: '2026-09-08T12:00:00',
        comment: 'Болит зуб [Заявка через чат]'
    });
    assert.equal(Object.hasOwn(payload, 'patientId'), false);
    assert.equal(Object.hasOwn(payload, 'status'), false);

    const calls = [];
    const response = { ok: true, status: 200 };
    const result = await submitChatAppointment(payload, {
        fetchImpl: async (url, options) => {
            calls.push({ url, options });
            return response;
        }
    });

    assert.equal(result, response);
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, '/api/AppointmentRequest');
    assert.equal(calls[0].options.credentials, 'same-origin');
    assert.equal(calls[0].options.headers.Authorization, undefined);
    assert.deepEqual(JSON.parse(calls[0].options.body), payload);
});

test('main installs the cookie-safe chat booking override', async () => {
    const text = await source('wwwroot/assets/js/main.js');

    assert.match(text, /installChatBookingCookieTransport\(ChatBot\)/);
});
