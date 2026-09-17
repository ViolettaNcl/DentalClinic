import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {
  FRONT_SEQUENCES,
  UPPER_SEQUENCES,
  LOWER_SEQUENCES,
  SIDE_SEQUENCES,
  PRESET_ASSETS,
  ASSET_VIEW,
  validateSmileAssetCatalog
} from '../../wwwroot/assets/js/managers/public/smileMeterModel.js';

const base = path.resolve('wwwroot/assets/images/Page_cosmetic-treatments/smile-meter');
const runtime = path.join(base, 'runtime');
const thumbs = path.join(base, 'thumbs');

const activeAssets = [...new Set([
  ...Object.values(FRONT_SEQUENCES).flat(),
  ...Object.values(UPPER_SEQUENCES).flat(),
  ...Object.values(LOWER_SEQUENCES).flat(),
  ...Object.values(SIDE_SEQUENCES).flat(),
  ...Object.values(PRESET_ASSETS).flatMap(view => Object.values(view))
])];

const retiredMisclassifiedNames = [
  'smile-3d-front-crowded-severe.png',
  'smile-3d-upper-crowded-medium.png',
  'smile-3d-lower-crowded-medium.png',
  'smile-3d-side-crowded.png'
];

function readPngSize(filePath) {
  const data = fs.readFileSync(filePath);
  assert.equal(data.toString('ascii', 1, 4), 'PNG');
  return { width: data.readUInt32BE(16), height: data.readUInt32BE(20), bytes: data.length };
}

test('every active Smile Meter source asset exists and has a canonical camera-view classification', () => {
  for (const asset of activeAssets) {
    assert.ok(fs.existsSync(path.join(base, asset)), `missing ${asset}`);
    assert.ok(['front', 'upper', 'lower', 'side'].includes(ASSET_VIEW[asset]), `missing view metadata for ${asset}`);
  }
  assert.ok(fs.existsSync(path.join(base, 'smile-ai-assistant.png')));
});

test('the four known camera-view filename mistakes were reclassified instead of staying active under the wrong view', () => {
  for (const legacy of retiredMisclassifiedNames) {
    assert.ok(!fs.existsSync(path.join(base, legacy)), `legacy misclassified source still active: ${legacy}`);
  }
  for (const corrected of [
    'smile-3d-side-crowded-severe.png',
    'smile-3d-side-crowded-soft.png',
    'smile-3d-upper-aligned.png',
    'smile-3d-upper-warm-natural.png'
  ]) {
    assert.ok(fs.existsSync(path.join(base, corrected)), `missing corrected source: ${corrected}`);
  }
});

test('runtime sequences and presets cannot cross camera views', () => {
  assert.deepEqual(validateSmileAssetCatalog(), []);
  const byView = {
    front: FRONT_SEQUENCES,
    upper: UPPER_SEQUENCES,
    lower: LOWER_SEQUENCES,
    side: SIDE_SEQUENCES
  };
  for (const [view, sequences] of Object.entries(byView)) {
    for (const asset of Object.values(sequences).flat()) {
      assert.equal(ASSET_VIEW[asset], view, `${asset} must stay in ${view}`);
      assert.ok(asset.includes(`-${view}-`), `${asset} canonical filename must match ${view}`);
    }
  }
});

test('active source renders remain healthy 2048x2048 PNG files', () => {
  for (const asset of activeAssets) {
    const meta = readPngSize(path.join(base, asset));
    assert.equal(meta.width, 2048, `${asset} width ${meta.width}`);
    assert.equal(meta.height, 2048, `${asset} height ${meta.height}`);
    assert.ok(meta.bytes > 50_000, `${asset} too small`);
  }
});

test('each active source has an optimized runtime WebP and thumbnail', () => {
  for (const asset of activeAssets) {
    const stem = asset.replace(/\.png$/i, '.webp');
    const runtimeFile = path.join(runtime, stem);
    const thumbFile = path.join(thumbs, stem);
    assert.ok(fs.existsSync(runtimeFile), `missing runtime ${stem}`);
    assert.ok(fs.existsSync(thumbFile), `missing thumbnail ${stem}`);
    assert.ok(fs.statSync(runtimeFile).size < fs.statSync(path.join(base, asset)).size, `runtime should be smaller: ${asset}`);
    assert.ok(fs.statSync(thumbFile).size < fs.statSync(runtimeFile).size, `thumbnail should be smaller: ${asset}`);
  }
});
