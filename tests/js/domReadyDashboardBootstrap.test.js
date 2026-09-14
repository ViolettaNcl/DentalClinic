import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { runWhenDomReady } from '../../wwwroot/assets/js/core/domReady.js';

test('runs an initializer immediately when an async module loads after DOMContentLoaded', () => {
    let calls = 0;
    const documentRef = {
        readyState: 'complete',
        addEventListener() {
            assert.fail('a completed document must not wait for another DOMContentLoaded event');
        }
    };

    runWhenDomReady(() => { calls += 1; }, documentRef);

    assert.equal(calls, 1);
});

test('waits exactly once while the document is still loading', () => {
    let listener;
    let options;
    let calls = 0;
    const documentRef = {
        readyState: 'loading',
        addEventListener(name, callback, suppliedOptions) {
            assert.equal(name, 'DOMContentLoaded');
            listener = callback;
            options = suppliedOptions;
        }
    };

    runWhenDomReady(() => { calls += 1; }, documentRef);

    assert.equal(calls, 0);
    assert.deepEqual(options, { once: true });
    listener();
    assert.equal(calls, 1);
});

test('patient and doctor dashboards use the late-import-safe initializer', async () => {
    const files = [
        '../../wwwroot/assets/js/managers/patient/patientDashboard.js',
        '../../wwwroot/assets/js/managers/public/myReviews.js',
        '../../wwwroot/assets/js/managers/admin/doctorsManager.js'
    ];

    for (const file of files) {
        const source = await readFile(new URL(file, import.meta.url), 'utf8');
        assert.match(source, /runWhenDomReady\s*\(/, file);
    }
});
