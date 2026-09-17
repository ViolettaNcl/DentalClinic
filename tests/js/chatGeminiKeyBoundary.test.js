import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '../..');
const aiService = fs.readFileSync(path.join(root, 'Services', 'DentaAiService.cs'), 'utf8');
const keyHandler = fs.readFileSync(path.join(root, 'Services', 'GeminiApiKeyHandler.cs'), 'utf8');

test('Denta Gemini requests keep secrets out of URLs and use the protected header path', () => {
    assert.doesNotMatch(aiService, /\?key=/);
    assert.doesNotMatch(aiService, /Gemini:ApiKey[^\n]*https:\/\//);
    assert.match(aiService, /models\/\{model\}:generateContent/);
    assert.match(aiService, /"gemini-3\.8-flash"/);

    assert.match(keyHandler, /TryAddWithoutValidation\("x-goog-api-key", apiKey\)/);
    assert.match(keyHandler, /StartsWith\("key="/);
});
