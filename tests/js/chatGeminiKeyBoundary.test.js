import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, '../..');

const aiService = fs.readFileSync(
    path.join(root, 'Services', 'DentaAiService.cs'),
    'utf8'
);

const keyHandler = fs.readFileSync(
    path.join(root, 'Services', 'GeminiApiKeyHandler.cs'),
    'utf8'
);


test('Denta Gemini requests keep secrets out of URLs and use the protected header path', () => {
    assert.doesNotMatch(
        aiService,
        /\?key=/
    );

    assert.doesNotMatch(
        aiService,
        /Gemini:ApiKey[^\n]*https:\/\//
    );

    assert.match(
        aiService,
        /models\/\{model\}:generateContent/
    );

    assert.match(
        aiService,
        /"gemini-3\.8-flash"/
    );


    // Security boundary:
    // API key must be passed through the protected Gemini header.
    // Allow normal C# formatting with line breaks and spaces.
    assert.match(
        keyHandler,
        /TryAddWithoutValidation\s*\(\s*"x-goog-api-key"\s*,\s*apiKey\s*\)/
    );


    // Security boundary:
    // Legacy ?key= query parameters must be filtered.
    assert.match(
        keyHandler,
        /StartsWith\s*\(\s*"key="\s*,/
    );
});