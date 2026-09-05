import test from 'node:test';
import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../../wwwroot');
const i18nDir = path.join(root, 'assets/i18n');
const languages = ['ru', 'en', 'fr', 'el', 'ar'];

async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) files.push(...await walk(full));
    else files.push(full);
  }
  return files;
}

async function loadDictionary(code) {
  const raw = await readFile(path.join(i18nDir, `${code}.json`), 'utf8');
  return JSON.parse(raw.replace(/^\uFEFF/, ''));
}

test('every data-i18n key used by HTML exists in every supported dictionary', async () => {
  const dictionaries = Object.fromEntries(
    await Promise.all(languages.map(async code => [code, await loadDictionary(code)]))
  );
  const htmlFiles = (await walk(root)).filter(file => file.endsWith('.html'));
  const usage = new Map();
  const attr = /data-i18n(?:-placeholder|-aria-label|-title|-alt|-value)?=["']([^"']+)["']/g;

  for (const file of htmlFiles) {
    const html = await readFile(file, 'utf8');
    for (const match of html.matchAll(attr)) {
      const key = match[1].trim();
      if (!key) continue;
      if (!usage.has(key)) usage.set(key, []);
      usage.get(key).push(path.relative(root, file));
    }
  }

  assert.ok(usage.size > 50, 'Expected substantial localized HTML coverage');

  for (const code of languages) {
    const missing = [...usage.keys()].filter(key => !(key in dictionaries[code]));
    assert.deepEqual(
      missing,
      [],
      `${code}: HTML uses missing i18n keys: ${missing.map(key => `${key} (${usage.get(key)?.[0]})`).join(', ')}`
    );
  }
});
