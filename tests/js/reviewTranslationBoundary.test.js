import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('review translation client sends only review identity and target language', async () => {
    const text = await source('wwwroot/assets/js/services/reviewTranslate.js');
    const normalized = text.replace(/\s+/g, ' ');

    assert.ok(normalized.includes('JSON.stringify({ reviewId: id, targetLang: lang })'));
    assert.doesNotMatch(normalized, /JSON\.stringify\(\{[^}]*text:\s*originalText/);
});

test('review translation endpoint owns text, origin policy, cache key and Gemini secret transport', async () => {
    const text = await source('Controllers/ReviewController.cs');
    const dtoStart = text.indexOf('public sealed class TranslateReviewRequest');
    const endpointStart = text.indexOf('[HttpPost("translate")]', dtoStart);
    const endpointEnd = text.indexOf('internal static string BuildReviewTranslationCacheKey', endpointStart);

    assert.notEqual(dtoStart, -1);
    assert.notEqual(endpointStart, -1);
    assert.notEqual(endpointEnd, -1);

    const dto = text.slice(dtoStart, endpointStart);
    const endpoint = text.slice(endpointStart, endpointEnd);

    assert.match(dto, /public\s+int\s+ReviewId\s*\{/);
    assert.match(dto, /public\s+string\s+TargetLang\s*\{/);
    assert.doesNotMatch(dto, /public\s+string\s+Text\s*\{/);

    // Scope source assertions to this endpoint. ReviewController legitimately uses
    // req.Text in the separate authenticated review-creation endpoint.
    assert.doesNotMatch(endpoint, /req\.Text/);
    assert.doesNotMatch(endpoint, /GetHashCode\(\)/);
    assert.match(endpoint, /AsNoTracking\(\)/);
    assert.match(endpoint, /IsAllowedOrigin\(\)/);
    assert.match(text, /SHA256\.HashData/);
    assert.match(endpoint, /generateContent\?key=compat/);
});

test('default HttpClient is protected by GeminiApiKeyHandler', async () => {
    const program = await source('Program.cs');

    assert.match(program, /AddTransient<GeminiApiKeyHandler>\(\)/);
    assert.match(
        program,
        /AddHttpClient\(string\.Empty\)[\s\S]*?AddHttpMessageHandler<GeminiApiKeyHandler>\(\)/
    );
});
