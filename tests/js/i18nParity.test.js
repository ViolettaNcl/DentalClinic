import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const i18nDir = path.resolve(here, '../../wwwroot/assets/i18n');
const languages = ['ru', 'en', 'fr', 'el', 'ar'];

async function load(code) {
  const raw = await readFile(path.join(i18nDir, `${code}.json`), 'utf8');
  return JSON.parse(raw.replace(/^\uFEFF/, ''));
}

test('all localization dictionaries have the same keys and no blank values', async () => {
  const dictionaries = Object.fromEntries(
    await Promise.all(languages.map(async code => [code, await load(code)]))
  );

  const referenceKeys = Object.keys(dictionaries.ru).sort();
  assert.ok(referenceKeys.length > 100, 'Russian reference dictionary should be substantial');

  for (const code of languages) {
    const dict = dictionaries[code];
    const keys = Object.keys(dict).sort();
    const missing = referenceKeys.filter(key => !(key in dict));
    const extra = keys.filter(key => !(key in dictionaries.ru));
    const blank = keys.filter(key => typeof dict[key] !== 'string' || dict[key].trim() === '');

    assert.deepEqual(missing, [], `${code}: missing localization keys: ${missing.join(', ')}`);
    assert.deepEqual(extra, [], `${code}: unexpected localization keys: ${extra.join(', ')}`);
    assert.deepEqual(blank, [], `${code}: blank/non-string localization values: ${blank.join(', ')}`);
  }
});
