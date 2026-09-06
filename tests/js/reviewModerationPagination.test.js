import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);

async function source(path) {
    return readFile(new URL(path, root), 'utf8');
}

test('review moderation loads bounded server pages instead of legacy full arrays', async () => {
    const src = await source('wwwroot/assets/js/managers/admin/reviewModeration.js');

    assert.match(src, /\/review\/admin\/list\/\$\{encodeURIComponent\(key\)\}\?page=\$\{page\}&pageSize=\$\{PAGE_SIZE\}/);
    assert.doesNotMatch(src, /\/review\/admin\/pending'/);
    assert.doesNotMatch(src, /\/review\/admin\/approved'/);
    assert.doesNotMatch(src, /\/review\/admin\/rejected'/);
    assert.match(src, /totalItems:\s*total/);
    assert.match(src, /onPageChange:\s*\(p\)\s*=>\s*this\._loadTab\(key, p\)/);
});
