const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '../..');
const chat = fs.readFileSync(path.join(root, 'Controllers', 'ChatController.cs'), 'utf8');

test('chat Gemini URLs never interpolate the configured secret', () => {
    assert.doesNotMatch(chat, /key=\{apiKey\}/);
    assert.match(chat, /:generateContent\?key=compat/);
    assert.match(chat, /:streamGenerateContent\?alt=sse&key=compat/);
});
