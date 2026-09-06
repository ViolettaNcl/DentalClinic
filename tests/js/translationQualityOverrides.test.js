import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {
    TRANSLATION_QUALITY_OVERRIDES,
    applyTranslationQualityOverrides,
} from '../../wwwroot/assets/js/core/translationQualityOverrides.js';

const supported = ['ru', 'en', 'fr', 'el', 'ar'];

function readDictionary(lang) {
    const url = new URL(`../../wwwroot/assets/i18n/${lang}.json`, import.meta.url);
    return JSON.parse(fs.readFileSync(url, 'utf8').replace(/^\uFEFF/, ''));
}

test('reviewed overrides only replace existing dictionary keys', () => {
    for (const lang of supported) {
        const dictionary = readDictionary(lang);
        const overrides = TRANSLATION_QUALITY_OVERRIDES[lang] || {};
        assert.ok(Object.keys(overrides).length > 0, `${lang} must have reviewed corrections`);

        for (const [key, value] of Object.entries(overrides)) {
            assert.ok(Object.hasOwn(dictionary, key), `${lang}.${key} must exist in the base dictionary`);
            assert.equal(typeof value, 'string');
            assert.ok(value.trim().length > 0, `${lang}.${key} must not be empty`);
        }
    }
});

test('correction layer does not mutate the loaded base dictionary', () => {
    const base = { nav_doctors: 'Doctors', untouched: 'Keep me' };
    const merged = applyTranslationQualityOverrides('en', base);

    assert.equal(base.nav_doctors, 'Doctors');
    assert.equal(merged.nav_doctors, 'Dentists');
    assert.equal(merged.untouched, 'Keep me');
    assert.notEqual(merged, base);
});

test('reviewed clinical copy avoids absolute pain-free promises', () => {
    const forbidden = [
        /безболезнен/i,
        /без боли/i,
        /painless/i,
        /no pain/i,
        /indolore/i,
        /sans douleur/i,
        /ανώδυν/i,
        /χωρίς πόνο/i,
        /غير مؤلم/i,
        /بدون ألم/i,
    ];

    for (const lang of supported) {
        const overrides = TRANSLATION_QUALITY_OVERRIDES[lang] || {};
        for (const key of ['about_p2', 'doc1_approach_text']) {
            const value = overrides[key];
            assert.ok(value, `${lang}.${key} must be explicitly reviewed`);
            for (const pattern of forbidden)
                assert.doesNotMatch(value, pattern, `${lang}.${key} contains an absolute pain-free claim`);
        }
    }
});

test('dental profession and core service terminology is localized precisely', () => {
    assert.equal(TRANSLATION_QUALITY_OVERRIDES.en.nav_doctors, 'Dentists');
    assert.equal(TRANSLATION_QUALITY_OVERRIDES.fr.nav_doctors, 'Dentistes');
    assert.equal(TRANSLATION_QUALITY_OVERRIDES.fr.svc_bridges, 'Ponts dentaires');
    assert.equal(TRANSLATION_QUALITY_OVERRIDES.el.nav_doctors, 'Οδοντίατροι');
    assert.equal(TRANSLATION_QUALITY_OVERRIDES.ar.nav_doctors, 'أطباء الأسنان');
    assert.equal(TRANSLATION_QUALITY_OVERRIDES.ar.svc_implants, 'زراعة الأسنان');
});
