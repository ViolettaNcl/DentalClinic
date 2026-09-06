import test from 'node:test';
import assert from 'node:assert/strict';
import { terminateCookieSession } from '../../wwwroot/assets/js/core/sessionTermination.js';

test('cookie session termination expires server session before cleanup and redirect', async () => {
  const calls = [];

  await terminateCookieSession({
    requestLogout: async () => { calls.push('server-logout'); },
    clearSession: () => { calls.push('clear-session'); },
    redirect: url => { calls.push(`redirect:${url}`); }
  });

  assert.deepEqual(calls, [
    'server-logout',
    'clear-session',
    'redirect:/index.html'
  ]);
});

test('failed server logout leaves local browser session untouched', async () => {
  const calls = [];

  await assert.rejects(() => terminateCookieSession({
    requestLogout: async () => {
      calls.push('server-logout');
      throw new Error('network down');
    },
    clearSession: () => { calls.push('clear-session'); },
    redirect: url => { calls.push(`redirect:${url}`); }
  }), /network down/);

  assert.deepEqual(calls, ['server-logout']);
});

test('cookie session termination validates required handlers', async () => {
  await assert.rejects(
    () => terminateCookieSession({ requestLogout: async () => {}, clearSession: null, redirect: () => {} }),
    /requires logout, cleanup and redirect handlers/
  );
});
