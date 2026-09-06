import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { escapeHtmlAttribute } from '../../wwwroot/assets/js/services/htmlAttributeSafety.js';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('attribute escaping neutralizes quote breakout and event handlers', () => {
    const payload = '" autofocus onfocus="alert(1)&<tag>';
    const escaped = escapeHtmlAttribute(payload);

    assert.equal(
        escaped,
        '&quot; autofocus onfocus=&quot;alert(1)&amp;&lt;tag&gt;'
    );
    assert.doesNotMatch(escaped, /(^|[^&])"/);
    assert.doesNotMatch(escaped, /</);
});

test('public review renderer uses attribute-safe escaping before DOMContentLoaded', async () => {
    const main = await source('wwwroot/assets/js/main.js');
    const reviews = await source('wwwroot/assets/js/managers/public/reviewsManager.js');

    assert.match(reviews, /data-original-text="\$\{this\._escape\(r\.text\)\}"/);
    assert.match(main, /import\s+\{\s*PublicReviewsManager\s*\}/);
    assert.match(main, /import\s+\{\s*escapeHtmlAttribute\s*\}/);
    assert.match(main, /PublicReviewsManager\.prototype\._escape\s*=\s*escapeHtmlAttribute/);
    assert.ok(
        main.indexOf('PublicReviewsManager.prototype._escape') <
        main.indexOf("document.addEventListener('DOMContentLoaded'")
    );
});

test('patient review renderer is patched after server session bootstrap', async () => {
    const entry = await source('wwwroot/assets/js/managers/patient/patientDashboardEntry.js');
    const reviews = await source('wwwroot/assets/js/managers/public/myReviews.js');

    assert.match(reviews, /data-original-text="\$\{this\._escape\(r\.text\)\}"/);
    assert.match(entry, /import\s+\{\s*escapeHtmlAttribute\s*\}/);
    assert.match(entry, /const\s+\{\s*MyReviewsManager\s*\}\s*=\s*await\s+import\('\.\.\/public\/myReviews\.js'\)/);
    assert.match(entry, /MyReviewsManager\.prototype\._escape\s*=\s*escapeHtmlAttribute/);
    assert.ok(
        entry.indexOf("requireServerSession('patient')") <
        entry.indexOf("import('../public/myReviews.js')")
    );
});
