import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import {
    buildClinicDirectionsEmbedUrl,
    buildClinicDirectionsUrl,
    buildClinicMapEmbedUrl,
    estimateTravelMinutes,
    predictAdaptiveRoute,
    recommendTravelMode,
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
        'https://www.google.com/maps/dir/?api=1&destination=%D0%92%D0%BE%D0%BB%D0%B3%D0%BE%D0%B3%D1%80%D0%B0%D0%B4%2C+%D0%BF%D1%80%D0%BE%D1%81%D0%BF%D0%B5%D0%BA%D1%82+%D0%9B%D0%B5%D0%BD%D0%B8%D0%BD%D0%B0%2C+1&travelmode=driving'
    );
});

test('builds directions inside the same embedded map', () => {
    const target = resolveClinicMapTarget({ hasCoordinates: true, latitude: 48.7, longitude: 44.5 });
    const url = new URL(buildClinicDirectionsEmbedUrl(target, '48.71,44.51', 'walking'));

    assert.equal(url.origin, 'https://www.google.com');
    assert.equal(url.pathname, '/maps');
    assert.equal(url.searchParams.get('output'), 'embed');
    assert.equal(url.searchParams.get('saddr'), '48.71,44.51');
    assert.equal(url.searchParams.get('daddr'), '48.7,44.5');
    assert.equal(url.searchParams.get('dirflg'), 'w');
});

test('embedded route requires a start point and never invents one', () => {
    const target = resolveClinicMapTarget({ hasCoordinates: true, latitude: 48.7, longitude: 44.5 });
    assert.equal(buildClinicDirectionsEmbedUrl(target, null), null);
    assert.equal(buildClinicDirectionsEmbedUrl(target, '   '), null);
    assert.equal(buildClinicDirectionsEmbedUrl(null, '48.71,44.51'), null);
});

test('embedded route maps supported travel modes to map direction flags', () => {
    const target = { type: 'address', value: 'Clinic address' };
    const flag = mode => new URL(buildClinicDirectionsEmbedUrl(target, 'Start', mode)).searchParams.get('dirflg');
    assert.equal(flag('driving'), 'd');
    assert.equal(flag('walking'), 'w');
    assert.equal(flag('transit'), 'r');
    assert.equal(flag('bicycling'), 'b');
    assert.equal(flag('unknown'), 'd');
});

test('does not invent a map destination without a configured address or coordinates', () => {
    assert.equal(resolveClinicMapTarget({ hasCoordinates: false, address: '  ' }), null);
    assert.equal(buildClinicMapEmbedUrl(null), null);
    assert.equal(buildClinicDirectionsUrl(null), null);
    assert.equal(buildClinicDirectionsEmbedUrl(null, 'Start'), null);
});

test('route controls are outside the interactive map and do not navigate away', async () => {
    const html = await readFile(new URL('../../wwwroot/pages/contact.html', import.meta.url), 'utf8');
    const script = await readFile(new URL('../../wwwroot/assets/js/managers/public/contactPage.js', import.meta.url), 'utf8');

    const mapStart = html.indexOf('<div class="map-container">');
    const mapEnd = html.indexOf('</div>', html.indexOf('</iframe>', mapStart));
    const mapBlock = html.slice(mapStart, mapEnd);

    assert.doesNotMatch(mapBlock, /smart-map-panel|route-dock-toggle|route-build-on-map/);
    assert.match(html, /<aside class="route-companion"/);
    assert.match(script, /buildClinicDirectionsEmbedUrl/);
    assert.doesNotMatch(script, /window\.open|target\s*=\s*['_"]_blank/);
});

test('smart route recommends a practical mode by distance', () => {
    assert.equal(recommendTravelMode(1.2), 'walking');
    assert.equal(recommendTravelMode(6), 'transit');
    assert.equal(recommendTravelMode(18), 'driving');
    assert.equal(estimateTravelMinutes(1.2, 'walking') > 0, true);
    assert.equal(estimateTravelMinutes(18, 'driving') > 0, true);
});


test('local adaptive route predictor returns a bounded ETA and confidence', () => {
    const offPeak = predictAdaptiveRoute(8, 'driving', new Date(2026, 8, 16, 13, 0));
    const peak = predictAdaptiveRoute(8, 'driving', new Date(2026, 8, 16, 17, 30));
    assert.equal(offPeak.model, 'adaptive-local-v1');
    assert.equal(offPeak.confidence >= 72 && offPeak.confidence <= 96, true);
    assert.equal(peak.minutes >= offPeak.minutes, true);
    assert.equal(predictAdaptiveRoute(-1, 'walking'), null);
});
