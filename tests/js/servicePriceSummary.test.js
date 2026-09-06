import test from 'node:test';
import assert from 'node:assert/strict';
import {
    buildStartingPricesByPage,
    formatStartingPrice,
} from '../../wwwroot/assets/js/managers/public/servicePriceSummary.js';

test('builds the lowest active starting price for each safe service page', () => {
    const result = buildStartingPricesByPage([
        { pageUrl: '/pages/services/implants.html', priceFrom: 45000, isActive: true },
        { pageUrl: '/pages/services/implants.html', priceFrom: 35000, isActive: true },
        { pageUrl: '/pages/services/implants.html', priceFrom: 10000, isActive: false },
        { pageUrl: '/pages/services/fillings.html', priceFrom: 5000, isActive: true },
    ]);

    assert.deepEqual(result, {
        '/pages/services/implants.html': 35000,
        '/pages/services/fillings.html': 5000,
    });
});

test('ignores malformed prices and unsafe page links', () => {
    const result = buildStartingPricesByPage([
        { pageUrl: 'https://evil.example/implants', priceFrom: 1 },
        { pageUrl: '/pages/../secret', priceFrom: 1 },
        { pageUrl: '/pages/services/crowns.html', priceFrom: -1 },
        { pageUrl: '/pages/services/crowns.html', priceFrom: 'not-a-price' },
        null,
    ]);

    assert.deepEqual(result, {});
});

test('formats starting prices in every supported UI language', () => {
    const samples = {
        ru: 'от',
        en: 'from',
        fr: 'à partir de',
        el: 'από',
        ar: 'ابتداءً من',
    };

    for (const [lang, prefix] of Object.entries(samples)) {
        const text = formatStartingPrice(35000, lang);
        assert.ok(text.startsWith(prefix), `${lang}: expected prefix ${prefix}, got ${text}`);
        assert.ok(text.endsWith('₽'), `${lang}: expected RUB symbol, got ${text}`);
    }
});

test('returns empty text for invalid public prices', () => {
    assert.equal(formatStartingPrice(-1, 'ru'), '');
    assert.equal(formatStartingPrice(Number.NaN, 'en'), '');
});
