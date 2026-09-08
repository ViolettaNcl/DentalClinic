import test from 'node:test';
import assert from 'node:assert/strict';
import {
    SUPPORTED_FEEDBACK_LANGUAGES,
    localizedFeedback,
    localizedFeedbackKeys,
    normalizeFeedbackLanguage
} from '../../wwwroot/assets/js/core/localizedFeedback.js';

const expectedKeys = [
    'appointmentPhoneRequired',
    'appointmentSubmitError',
    'appointmentSuccess',
    'passwordRequirements'
];

test('synchronous validation and form feedback is complete in every supported language', () => {
    assert.deepEqual([...SUPPORTED_FEEDBACK_LANGUAGES], ['ru', 'en', 'fr', 'el', 'ar']);
    assert.deepEqual(localizedFeedbackKeys().sort(), expectedKeys);

    for (const lang of SUPPORTED_FEEDBACK_LANGUAGES) {
        for (const key of expectedKeys) {
            const message = localizedFeedback(key, lang);
            assert.equal(typeof message, 'string');
            assert.ok(message.trim().length > 0, `${lang}: blank localized feedback for ${key}`);
            assert.notEqual(message, key, `${lang}: unresolved localized feedback key ${key}`);
        }

        assert.match(localizedFeedback('passwordRequirements', lang), /8/);
    }
});

test('feedback locale normalization is predictable and fail-safe', () => {
    assert.equal(normalizeFeedbackLanguage('EN-us'), 'en');
    assert.equal(normalizeFeedbackLanguage(' fr-FR '), 'fr');
    assert.equal(normalizeFeedbackLanguage('unknown'), 'ru');
    assert.equal(localizedFeedback('appointmentPhoneRequired', 'unknown'), localizedFeedback('appointmentPhoneRequired', 'ru'));
    assert.equal(localizedFeedback('unknownKey', 'en'), 'unknownKey');
});
