import test from 'node:test';
import assert from 'node:assert/strict';
import { terminateAdminSession } from '../../wwwroot/assets/js/core/adminSession.js';

test('admin session expires server cookie before local cleanup and redirect', async () => {
    const calls = [];

    await terminateAdminSession({
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

test('admin session does not clear local state when server logout fails', async () => {
    const calls = [];

    await assert.rejects(() => terminateAdminSession({
        requestLogout: async () => {
            calls.push('server-logout');
            throw new Error('network down');
        },
        clearSession: () => { calls.push('clear-session'); },
        redirect: url => { calls.push(`redirect:${url}`); }
    }), /network down/);

    assert.deepEqual(calls, ['server-logout']);
});
