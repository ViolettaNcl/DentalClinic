import test from 'node:test';
import assert from 'node:assert/strict';
import {
    escapeChatBookingHtml,
    installChatBookingCookieTransport
} from '../../wwwroot/assets/js/core/chatBookingTransport.js';

test('booking confirmation escapes HTML from user-controlled fields', () => {
    const malicious = `<img src=x onerror="globalThis.pwned=1">'&`;
    const escaped = escapeChatBookingHtml(malicious);

    assert.equal(
        escaped,
        '&lt;img src=x onerror=&quot;globalThis.pwned=1&quot;&gt;&#39;&amp;'
    );
    assert.doesNotMatch(escaped, /<img/i);
    assert.doesNotMatch(escaped, /onerror="/i);
});

test('booking hardening installs a dedicated safe confirmation renderer', () => {
    class FakeChatBot {}

    installChatBookingCookieTransport(FakeChatBot);

    assert.equal(typeof FakeChatBot.prototype._showBookingConfirmation, 'function');
    assert.equal(typeof FakeChatBot.prototype._submitBooking, 'function');
    assert.match(
        FakeChatBot.prototype._showBookingConfirmation.toString(),
        /escapeChatBookingHtml\(booking\.name\)/
    );
    assert.match(
        FakeChatBot.prototype._showBookingConfirmation.toString(),
        /escapeChatBookingHtml\(booking\.comment\)/
    );
});
