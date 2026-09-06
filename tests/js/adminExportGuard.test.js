import test from 'node:test';
import assert from 'node:assert/strict';
import { requestAdminExport } from '../../wwwroot/assets/js/managers/admin/adminExportGuard.js';

test('admin exports rely on same-origin cookie without Authorization header', async () => {
    const calls = [];
    const response = { ok: true, status: 200 };

    const result = await requestAdminExport('/api/adminstats/export/xlsx', {
        fetchImpl: async (url, options) => {
            calls.push({ url, options });
            return response;
        }
    });

    assert.equal(result, response);
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, '/api/adminstats/export/xlsx');
    assert.equal(calls[0].options.credentials, 'same-origin');
    assert.equal(calls[0].options.headers.Authorization, undefined);
});

test('admin export surfaces an expired cookie session distinctly', async () => {
    await assert.rejects(
        () => requestAdminExport('/api/adminstats/export/report', {
            fetchImpl: async () => ({ ok: false, status: 401 })
        }),
        error => error?.status === 401 && /Сеанс администратора завершён/.test(error.message)
    );
});
