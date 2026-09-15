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
        'https://www.google.com/maps/dir/?api=1&destination=%D0%92%D0%BE%D0%BB%D0%B3%D0%BE%D0%B3%D1%80%D0%B0%D0%B4%2C+%D0%BF%D1%80%D0%BE%D1%81%D0%BF%D0%B5%D0%BA%D1%82+%D0%9B%D0%B5%D0%BD%D0%B8%D0%BD%D0%B0%2C+1&travelmode=driving'
    );
});

test('keeps the driving-mode route when the browser provides an origin', () => {
    const target = resolveClinicMapTarget({
        hasCoordinates: true,
        latitude: 48.7,
        longitude: 44.5
    });

    assert.equal(
        buildClinicDirectionsUrl(target, '48.71,44.51'),
        'https://www.google.com/maps/dir/?api=1&destination=48.7%2C44.5&origin=48.71%2C44.51&travelmode=driving'
    );
});

test('does not invent a map destination without a configured address or coordinates', () => {
    assert.equal(resolveClinicMapTarget({ hasCoordinates: false, address: '  ' }), null);
    assert.equal(buildClinicMapEmbedUrl(null), null);
    assert.equal(buildClinicDirectionsUrl(null), null);
});

test('coordinate routes also request driving when geolocation is unavailable', () => {
    const target = resolveClinicMapTarget({ hasCoordinates: true, latitude: 48.7, longitude: 44.5 });
    const route = new URL(buildClinicDirectionsUrl(target));
    assert.equal(route.searchParams.get('travelmode'), 'driving');
    assert.equal(route.searchParams.has('origin'), false);
});

test('clicking a child of the route button keeps the address-only result visible', async () => {
    const { readFile } = await import('node:fs/promises');
    const { runInNewContext } = await import('node:vm');
    const source = await readFile(new URL('../../wwwroot/assets/js/managers/public/contactPage.js', import.meta.url), 'utf8');
    const functions = source.slice(source.indexOf('function renderRouteResult'), source.indexOf('async function initContactPage'));
    const child = {};
    let buttonClick;
    let documentClick;
    let visible = false;
    const links = [];
    const result = {
        replaceChildren() {}, appendChild: node => { links.push(node); }, contains: () => false,
        classList: { add: () => { visible = true; }, remove: () => { visible = false; } }
    };
    const button = {
        querySelector: () => null,
        addEventListener: (_, callback) => { buttonClick = callback; },
        contains: target => target === child || target === button
    };
    const document = {
        getElementById: id => id === 'route-fab' ? button : result,
        createTextNode: text => ({ text }), createElement: tag => ({ tag }),
        addEventListener: (_, callback) => { documentClick = callback; }
    };
    runInNewContext(functions + '\ninitRouteBuilder({ hasCoordinates: false }, { type: "address", value: "Clinic address" });', {
        document, navigator: {}, buildClinicDirectionsUrl, t: (_, fallback) => fallback
    });
    buttonClick();
    documentClick({ target: child });
    assert.equal(visible, true);
    assert.equal(new URL(links.find(node => node.tag === 'a').href).searchParams.get('travelmode'), 'driving');
    documentClick({ target: {} });
    assert.equal(visible, false);
});
