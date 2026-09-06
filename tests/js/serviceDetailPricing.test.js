import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildDetailPriceSlots,
    formatDetailServicePrice,
    formatDetailServicePriceParts,
} from '../../wwwroot/assets/js/managers/public/serviceDetailPricing.js';

test('maps active services to stable detail-card slots by sort order', () => {
    const services = [
        { id: 11, pageUrl: '/pages/services/implants.html', sortOrder: 1, priceFrom: 35000, isActive: true },
        { id: 12, pageUrl: '/pages/services/implants.html', sortOrder: 2, priceFrom: 55000, isActive: true },
        { id: 13, pageUrl: '/pages/services/implants.html', sortOrder: 3, priceFrom: 250000, isActive: false },
        { id: 14, pageUrl: '/pages/services/crowns.html', sortOrder: 1, priceFrom: 12000, isActive: true },
        { id: 15, pageUrl: 'https://evil.example/x', sortOrder: 1, priceFrom: 1, isActive: true },
        { id: 16, pageUrl: '/pages/services/implants.html', sortOrder: 2, priceFrom: 1, isActive: true },
        { id: 17, pageUrl: '/pages/services/implants.html', sortOrder: 0, priceFrom: 1, isActive: true },
    ];

    const result = buildDetailPriceSlots(services, '/pages/services/implants.html?ref=test', 3);

    assert.deepEqual(result.map(item => [item.slotIndex, item.service.id]), [
        [0, 11],
        [1, 12],
    ]);
});

test('does not shift a later service into a disabled earlier slot', () => {
    const result = buildDetailPriceSlots([
        { id: 1, pageUrl: '/pages/services/crowns.html', sortOrder: 1, priceFrom: 12000, isActive: false },
        { id: 2, pageUrl: '/pages/services/crowns.html', sortOrder: 2, priceFrom: 18000, isActive: true },
    ], '/pages/services/crowns.html', 3);

    assert.equal(result.length, 1);
    assert.equal(result[0].slotIndex, 1);
    assert.equal(result[0].service.id, 2);
});

test('formats starting, ranged and exact prices without changing currency', () => {
    assert.match(formatDetailServicePrice({ priceFrom: 35000 }, 'ru'), /^от\s+35[\s\u00a0\u202f]?000\s+₽$/);
    assert.equal(formatDetailServicePrice({ priceFrom: 35000 }, 'en'), 'from 35,000 ₽');
    assert.equal(formatDetailServicePrice({ priceFrom: 2500, priceTo: 5000 }, 'en'), '2,500–5,000 ₽');
    assert.equal(formatDetailServicePrice({ priceFrom: 5000, priceTo: 5000 }, 'en'), '5,000 ₽');

    assert.deepEqual(formatDetailServicePriceParts({ priceFrom: 12000 }, 'en'), {
        amount: 'from 12,000',
        currency: '₽',
    });
});

test('rejects malformed price data and invalid page mappings', () => {
    assert.equal(formatDetailServicePrice({ priceFrom: -1 }, 'ru'), '');
    assert.equal(formatDetailServicePrice({ priceFrom: 100, priceTo: 99 }, 'ru'), '');
    assert.deepEqual(buildDetailPriceSlots([
        { id: 1, pageUrl: '/pages/services/../secret.html', sortOrder: 1, priceFrom: 100 },
        { id: 2, pageUrl: '/pages/services\\secret.html', sortOrder: 1, priceFrom: 100 },
        { id: 3, pageUrl: '//evil.example/pages/services/x.html', sortOrder: 1, priceFrom: 100 },
    ], '/pages/services/x.html', 3), []);
});
