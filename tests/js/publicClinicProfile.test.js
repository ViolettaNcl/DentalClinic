import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import {
    getPublicClinicProfile,
    normalizeProfile
} from '../../wwwroot/assets/js/core/publicClinicProfile.js';

test('public clinic profile does not coerce missing coordinates to zero', () => {
    for (const value of [null, undefined, '', '   ']) {
        const profile = normalizeProfile({ latitude: value, longitude: value });
        assert.equal(profile.hasCoordinates, false);
        assert.equal(profile.latitude, null);
        assert.equal(profile.longitude, null);
    }
});

test('public clinic profile accepts only a complete in-range coordinate pair', () => {
    const valid = normalizeProfile({
        phone: '  +357 22 000000  ',
        email: '  clinic@example.test ',
        address: '  Example address  ',
        hours: '  Mon-Sat 09:00-20:00 ',
        latitude: '34.7071',
        longitude: 33.0226
    });

    assert.equal(valid.phone, '+357 22 000000');
    assert.equal(valid.email, 'clinic@example.test');
    assert.equal(valid.address, 'Example address');
    assert.equal(valid.hours, 'Mon-Sat 09:00-20:00');
    assert.equal(valid.latitude, 34.7071);
    assert.equal(valid.longitude, 33.0226);
    assert.equal(valid.hasCoordinates, true);

    for (const candidate of [
        { latitude: 34.7, longitude: null },
        { latitude: null, longitude: 33.0 },
        { latitude: 91, longitude: 33.0 },
        { latitude: 34.7, longitude: 181 },
        { latitude: true, longitude: false }
    ]) {
        const profile = normalizeProfile(candidate);
        assert.equal(profile.hasCoordinates, false);
        assert.equal(profile.latitude, null);
        assert.equal(profile.longitude, null);
    }
});

test('public clinic profile fetch is same-origin and returns normalized data', async () => {
    let request;
    const profile = await getPublicClinicProfile({
        force: true,
        fetchImpl: async (url, options) => {
            request = { url, options };
            return {
                ok: true,
                async json() {
                    return { phone: ' +357 22 000000 ', latitude: 34.7, longitude: 33.0 };
                }
            };
        }
    });

    assert.equal(request.url, '/api/clinic/profile');
    assert.equal(request.options.credentials, 'same-origin');
    assert.equal(request.options.headers.Accept, 'application/json');
    assert.equal(profile.phone, '+357 22 000000');
    assert.equal(profile.hasCoordinates, true);
});

test('public runtime assets do not ship retired clinic facts', async () => {
    const runtimeSources = await Promise.all([
        readFile(new URL('../../wwwroot/pages/contact.html', import.meta.url), 'utf8'),
        readFile(new URL('../../wwwroot/index.html', import.meta.url), 'utf8'),
        readFile(new URL('../../wwwroot/assets/js/core/chatBot.js', import.meta.url), 'utf8')
    ]);

    for (const source of runtimeSources) {
        assert.doesNotMatch(source, /support@dentalclinic\.ru/i);
        assert.doesNotMatch(source, /499[- )]999[- ]99[- ]99/i);
        assert.doesNotMatch(source, /ул\.\s*Мира\s*,?\s*25/i);
        assert.doesNotMatch(source, /48\.709737|44\.516499/);
    }

    for (const locale of ['ru', 'en', 'fr', 'el', 'ar']) {
        const raw = await readFile(
            new URL(`../../wwwroot/assets/i18n/${locale}.json`, import.meta.url),
            'utf8');
        const dictionary = JSON.parse(raw.replace(/^\uFEFF/, ''));

        assert.doesNotMatch(raw, /499[- )]999[- ]99[- ]99/i);
        assert.equal(Object.hasOwn(dictionary, 'contact_card_address_text'), false);
        assert.equal(Object.hasOwn(dictionary, 'contact_hours_weekdays'), false);
        assert.equal(Object.hasOwn(dictionary, 'contact_hours_sunday'), false);
        assert.match(dictionary.patient_confirmed_call_required, /contact|контакт|επικοινων|اتصال/i);
        assert.match(dictionary.chat_booking_error, /contact|контакт|επικοινων|اتصال/i);
    }
});
