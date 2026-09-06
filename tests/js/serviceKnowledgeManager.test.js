import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildServicePayload,
    formatServicePrice,
    SERVICE_FIELD_LIMITS,
    SERVICE_MAX_PRICE,
} from '../../wwwroot/assets/js/managers/admin/serviceKnowledgeUtils.js';

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

test('accepts exact persistence boundaries used by the service API', () => {
    const result = buildServicePayload({
        category: 'A'.repeat(SERVICE_FIELD_LIMITS.category),
        name: 'B'.repeat(SERVICE_FIELD_LIMITS.name),
        description: 'D'.repeat(SERVICE_FIELD_LIMITS.description),
        unit: 'U'.repeat(SERVICE_FIELD_LIMITS.unit),
        keywords: 'K'.repeat(SERVICE_FIELD_LIMITS.keywords),
        pageUrl: '/pages/' + 'p'.repeat(SERVICE_FIELD_LIMITS.pageUrl - '/pages/'.length),
        priceFrom: String(SERVICE_MAX_PRICE).replace('.', ','),
        priceTo: String(SERVICE_MAX_PRICE),
        sortOrder: '0',
    });

    assert.equal(result.ok, true);
    assert.equal(result.payload.priceFrom, SERVICE_MAX_PRICE);
    assert.equal(result.payload.priceTo, SERVICE_MAX_PRICE);
});

test('rejects values that exceed database-backed field lengths', () => {
    const base = { category: 'A', name: 'B', priceFrom: '100' };

    for (const [field, max] of Object.entries(SERVICE_FIELD_LIMITS)) {
        const values = { ...base, [field]: 'x'.repeat(max + 1) };
        if (field === 'pageUrl') values[field] = '/pages/' + 'x'.repeat(max + 1 - '/pages/'.length);
        const result = buildServicePayload(values);
        assert.equal(result.ok, false, `${field} should reject values longer than ${max}`);
    }
});

test('rejects prices that SQL decimal(10,2) cannot represent exactly', () => {
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100000000' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '123.456' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', priceTo: '123,456' }).ok, false);
});

test('rejects invalid price ranges, links and sort orders', () => {
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '-1' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', priceTo: '99' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', pageUrl: 'https://evil.example/x' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', pageUrl: '/pages/../secret' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', sortOrder: '-1' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', sortOrder: '1.5' }).ok, false);
    assert.equal(buildServicePayload({ category: 'A', name: 'B', priceFrom: '100', sortOrder: 'abc' }).ok, false);
});

test('formats single and ranged prices for the admin table', () => {
    assert.match(formatServicePrice({ priceFrom: 5000 }), /5\s?000 ₽/);
    assert.match(formatServicePrice({ priceFrom: 5000, priceTo: 8000, unit: 'зуб' }), /5\s?000–8\s?000 ₽ \/ зуб/);
});
