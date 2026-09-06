import test from 'node:test';
import assert from 'node:assert/strict';
import { ApiError, apiFetch } from '../../wwwroot/assets/js/services/apiClient.js';

async function withGlobals({ fetchImpl, sessionStorageImpl }, fn) {
  const previousFetch = globalThis.fetch;
  const previousSessionStorage = globalThis.sessionStorage;
  const previousConsoleError = console.error;

  globalThis.fetch = fetchImpl;
  globalThis.sessionStorage = sessionStorageImpl;
  console.error = () => {};

  try {
    await fn();
  } finally {
    globalThis.fetch = previousFetch;
    if (previousSessionStorage === undefined) delete globalThis.sessionStorage;
    else globalThis.sessionStorage = previousSessionStorage;
    console.error = previousConsoleError;
  }
}

test('apiFetch preserves non-401 HTTP status and server message', async () => {
  await withGlobals({
    fetchImpl: async () => ({
      ok: false,
      status: 429,
      json: async () => ({ message: 'Too many requests' })
    }),
    sessionStorageImpl: { removeItem() {} }
  }, async () => {
    await assert.rejects(
      () => apiFetch('/auth/login', { method: 'POST' }),
      error => {
        assert.ok(error instanceof ApiError);
        assert.equal(error.status, 429);
        assert.equal(error.message, 'Too many requests');
        assert.deepEqual(error.payload, { message: 'Too many requests' });
        return true;
      }
    );
  });
});

test('apiFetch preserves 401 status and clears stale local session metadata', async () => {
  const removed = [];

  await withGlobals({
    fetchImpl: async () => ({
      ok: false,
      status: 401,
      json: async () => ({ message: 'Invalid credentials' })
    }),
    sessionStorageImpl: { removeItem: key => removed.push(key) }
  }, async () => {
    await assert.rejects(
      () => apiFetch('/auth/login', { method: 'POST' }),
      error => {
        assert.equal(error.status, 401);
        return true;
      }
    );
  });

  assert.deepEqual(removed.sort(), [
    'authToken',
    'patientEmail',
    'patientId',
    'patientName',
    'userRole'
  ].sort());
});

test('network failures remain distinguishable from HTTP authentication failures', async () => {
  await withGlobals({
    fetchImpl: async () => { throw new TypeError('network down'); },
    sessionStorageImpl: { removeItem() {} }
  }, async () => {
    await assert.rejects(
      () => apiFetch('/auth/login'),
      error => {
        assert.equal(error instanceof ApiError, false);
        assert.equal(error.status, undefined);
        return true;
      }
    );
  });
});
