import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('review translation client sends only review identity and target language', async () => {
    const text = await source('wwwroot/assets/js/services/reviewTranslate.js');

    assert.match(text, /JSON\.stringify\(\{\s*reviewId:\s*id,\s*targetLang:\s*lang\s*\}\)/);
    assert.doesNotMatch(text, /JSON\.stringify\(\{[^}]*text:\s*originalText/);
});

test('review translation endpoint owns text, origin policy, cache key and Gemini secret transport', async () => {
    const text = await source('Controllers/ReviewController.cs');

    assert.doesNotMatch(text, /public\s+string\s+Text\s*\{\s*get;\s*set;/);
    assert.doesNotMatch(text, /req\.Text/);
    assert.doesNotMatch(text, /GetHashCode\(\)/);
    assert.match(text, /AsNoTracking\(\)/);
    assert.match(text, /IsAllowedOrigin\(\)/);
    assert.match(text, /SHA256\.HashData/);
    assert.match(text, /generateContent\?key=compat/);
});
