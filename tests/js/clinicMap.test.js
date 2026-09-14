import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildClinicDirectionsUrl,
    buildClinicMapEmbedUrl,
    resolveClinicMapTarget
} from '../../wwwroot/assets/js/core/clinicMap.js';

test('uses exact clinic coordinates when they are configured', () => {
    const target = resolveClinicMapTarget({
        hasCoordinates: true,
        latitude: 48.7,
        longitude: 44.5,
        address: 'Fallback address'
    });

    assert.deepEqual(target, { type: 'coordinates', value: '48.7,44.5' });
    assert.equal(
        buildClinicMapEmbedUrl(target),
        'https://www.google.com/maps?q=48.7%2C44.5&output=embed'
    );
});

test('renders a clinic map from its configured address when coordinates are absent', () => {
    const target = resolveClinicMapTarget({
        hasCoordinates: false,
        address: '  Волгоград, проспект Ленина, 1  '
    });

    assert.deepEqual(target, {
        type: 'address',
        value: 'Волгоград, проспект Ленина, 1'
    });
    assert.equal(
        buildClinicDirectionsUrl(target),
        'https://www.google.com/maps/dir/?api=1&destination=%D0%92%D0%BE%D0%BB%D0%B3%D0%BE%D0%B3%D1%80%D0%B0%D0%B4%2C+%D0%BF%D1%80%D0%BE%D1%81%D0%BF%D0%B5%D0%BA%D1%82+%D0%9B%D0%B5%D0%BD%D0%B8%D0%BD%D0%B0%2C+1'
    );
});

test('does not invent a map destination without a configured address or coordinates', () => {
    assert.equal(resolveClinicMapTarget({ hasCoordinates: false, address: '  ' }), null);
    assert.equal(buildClinicMapEmbedUrl(null), null);
    assert.equal(buildClinicDirectionsUrl(null), null);
});
