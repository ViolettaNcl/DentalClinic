import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '../..');
const chat = fs.readFileSync(path.join(root, 'Controllers', 'ChatController.cs'), 'utf8');

test('chat Gemini URLs never interpolate the configured secret', () => {
    assert.doesNotMatch(chat, /key=\{apiKey\}/);
    assert.match(chat, /:generateContent\?key=compat/);
    assert.match(chat, /:streamGenerateContent\?alt=sse&key=compat/);
});
