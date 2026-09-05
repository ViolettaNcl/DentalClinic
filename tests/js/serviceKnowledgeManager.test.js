import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildServicePayload,
    formatServicePrice,
} from '../../wwwroot/assets/js/managers/admin/serviceKnowledgeManager.js';

test('builds a normalized create payload for Denta knowledge service', () => {
    const result = buildServicePayload({
        category: '  Имплантация ',
        name: ' Имплант ',
        priceFrom: '35000',
        priceTo: '55 000'.replace(' ', ''),
        unit: ' зуб ',
        keywords: ' имплант, all-on-4 ',
        pageUrl: '/pages/services/implants.html',
        description: ' описание ',
        sortOrder: '4',
    });

    assert.equal(result.ok, true);
    assert.deepEqual(result.payload, {
        category: 'Имплантация',
        name: 'Имплант',
        description: 'описание',
        priceFrom: 35000,
        priceTo: 55000,
        unit: 'зуб',
        keywords: 'имплант, all-on-4',
        pageUrl: '/pages/services/implants.html',
        sortOrder: 4,
    });
});

test('edit payload can explicitly clear an existing upper price', () => {
    const result = buildServicePayload({
        category: 'Терапия',
        name: 'Пломба',
        priceFrom: '5000',
        priceTo: '',
        isActive: true,
    }, { edit: true });

    assert.equal(result.ok, true);
    assert.equal(result.payload.clearPriceTo, true);
    assert.equal(result.payload.isActive, true);
    assert.equal('priceTo' in result.payload, false);
});

test('rejects invalid price ranges and external or traversal links', () => {
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '-1' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', priceTo: '99' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', pageUrl: 'https://evil.example/x' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', pageUrl: '/pages/../secret' }).ok, false);
});

test('formats single and ranged prices for the admin table', () => {
    assert.match(formatServicePrice({ priceFrom: 5000 }), /5\s?000 ₽/);
    assert.match(formatServicePrice({ priceFrom: 5000, priceTo: 8000, unit: 'зуб' }), /5\s?000–8\s?000 ₽ \/ зуб/);
});
