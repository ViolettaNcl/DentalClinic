import test from 'node:test';
import assert from 'node:assert/strict';
import {
    getServerSession,
    requireServerSession,
    resetSessionBootstrapCache,
    syncSessionMetadata
} from '../../wwwroot/assets/js/core/sessionBootstrap.js';

function createStorage(seed = {}) {
    const values = new Map(Object.entries(seed).map(([k, v]) => [k, String(v)]));
    return {
        getItem: key => values.has(key) ? values.get(key) : null,
        setItem: (key, value) => values.set(key, String(value)),
        removeItem: key => values.delete(key),
        snapshot: () => Object.fromEntries(values)
    };
}

test('syncSessionMetadata restores non-secret UI state and removes legacy token', () => {
    const storage = createStorage({ authToken: 'legacy-secret' });

    syncSessionMetadata({
        id: 42,
        name: 'Ирина',
        email: 'irina@example.com',
        role: 'Patient'
    }, storage);

    assert.deepEqual(storage.snapshot(), {
        patientId: '42',
        patientName: 'Ирина',
        patientEmail: 'irina@example.com',
        userRole: 'patient'
    });
});

test('anonymous server session clears stale tab metadata', async () => {
    resetSessionBootstrapCache();
    const storage = createStorage({
        patientId: 9,
        patientName: 'Stale',
        patientEmail: 'stale@example.com',
        userRole: 'patient',
        authToken: 'legacy'
    });

    const session = await getServerSession({
        force: true,
        request: async () => null,
        storage
    });

    assert.equal(session, null);
    assert.deepEqual(storage.snapshot(), {});
});

test('protected dashboard rejects a valid session with the wrong role', async () => {
    resetSessionBootstrapCache();
    const storage = createStorage();
    const redirects = [];

    const session = await requireServerSession('patient', {
        request: async () => ({
            id: 1,
            name: 'Администратор',
            email: 'admin@example.com',
            role: 'admin'
        }),
        storage,
        redirect: url => redirects.push(url)
    });

    assert.equal(session, null);
    assert.deepEqual(redirects, ['/index.html']);
    assert.deepEqual(storage.snapshot(), {});
});

test('protected dashboard restores matching session before continuing', async () => {
    resetSessionBootstrapCache();
    const storage = createStorage();
    const redirects = [];

    const session = await requireServerSession('patient', {
        request: async () => ({
            id: 7,
            name: 'Пациент',
            email: 'patient@example.com',
            role: 'patient'
        }),
        storage,
        redirect: url => redirects.push(url)
    });

    assert.equal(session.id, 7);
    assert.deepEqual(redirects, []);
    assert.equal(storage.getItem('patientId'), '7');
    assert.equal(storage.getItem('userRole'), 'patient');
});
