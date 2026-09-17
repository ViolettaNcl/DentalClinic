import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { FRONT_SEQUENCES, UPPER_SEQUENCES, LOWER_SEQUENCES, SIDE_SEQUENCES, DEFAULT_VALUES, defaultAssetForView, selectViewAsset } from '../../wwwroot/assets/js/managers/public/smileMeterModel.js';

const ROOT = process.cwd();
const HTML_PATH = path.join(ROOT, 'wwwroot/pages/services/cosmetic-treatments.html');
const CSS_PATH = path.join(ROOT, 'wwwroot/assets/css/components/smile-meter.css');
const JS_PATH = path.join(ROOT, 'wwwroot/assets/js/managers/public/smileMeter.js');
const IMAGE_DIR = path.join(ROOT, 'wwwroot/assets/images/Page_cosmetic-treatments/smile-meter');
const THUMB_DIR = path.join(IMAGE_DIR, 'thumbs');
const RUNTIME_DIR = path.join(IMAGE_DIR, 'runtime');

function pngInfo(filePath) {
    const fd = fs.openSync(filePath, 'r');
    try {
        const header = Buffer.alloc(26);
        const read = fs.readSync(fd, header, 0, header.length, 0);
        assert.equal(read, 26, `short PNG header: ${filePath}`);
        assert.deepEqual([...header.subarray(0, 8)], [137,80,78,71,13,10,26,10], `invalid PNG signature: ${filePath}`);
        return {
            width: header.readUInt32BE(16),
            height: header.readUInt32BE(20),
            bitDepth: header[24],
            colorType: header[25]
        };
    } finally {
        fs.closeSync(fd);
    }
}

test('Smile Meter has exactly one dental model image and no layered dental compositing markup', () => {
    const html = fs.readFileSync(HTML_PATH, 'utf8');
    assert.equal((html.match(/id="smile-model-image"/g) || []).length, 1);
    assert.equal((html.match(/id="smile-model-image-[ab]"/g) || []).length, 0);
    assert.equal((html.match(/smile-model-layer/g) || []).length, 0);
    for (const id of ['slider-white', 'slider-align', 'slider-shape']) {
        const input = html.match(new RegExp(`<input[^>]*id=\"${id}\"[^>]*>`))?.[0];
        assert.ok(input, `missing ${id}`);
        assert.match(input, /min=\"0\"/);
        assert.match(input, /max=\"100\"/);
    }
});

test('Smile Meter CSS and JS contain no legacy multi-layer renderer or forced scan flashes for manual changes', () => {
    const css = fs.readFileSync(CSS_PATH, 'utf8');
    const js = fs.readFileSync(JS_PATH, 'utf8');
    assert.doesNotMatch(css, /smile-model-layer/);
    assert.doesNotMatch(js, /modelLayer|smile-model-image-a|smile-model-image-b/);
    assert.match(js, /one visible dental image/i);
    assert.match(js, /const showScan = presetName && !reduceMotion;/);
});

test('all staged assets used by runtime exist, are high-resolution RGBA PNGs, and have thumbnails', () => {
    const assets = new Set([
        ...Object.values(FRONT_SEQUENCES).flat(),
        ...Object.values(UPPER_SEQUENCES).flat(),
        ...Object.values(LOWER_SEQUENCES).flat(),
        ...Object.values(SIDE_SEQUENCES).flat()
    ]);
    for (const asset of assets) {
        const fullPath = path.join(IMAGE_DIR, asset);
        assert.ok(fs.existsSync(fullPath), `missing asset: ${asset}`);
        assert.ok(fs.statSync(fullPath).size > 100_000, `asset unexpectedly small: ${asset}`);
        const info = pngInfo(fullPath);
        assert.equal(info.width, 2048, `${asset} width`);
        assert.equal(info.height, 2048, `${asset} height`);
        assert.equal(info.colorType, 6, `${asset} must be RGBA PNG`);
        const thumb = path.join(THUMB_DIR, asset.replace(/\.png$/i, '.webp'));
        assert.ok(fs.existsSync(thumb), `missing thumbnail: ${path.basename(thumb)}`);
        const runtime = path.join(RUNTIME_DIR, asset.replace(/\.png$/i, '.webp'));
        assert.ok(fs.existsSync(runtime), `missing optimized runtime asset: ${path.basename(runtime)}`);
        assert.ok(fs.statSync(runtime).size < fs.statSync(fullPath).size, `runtime asset should be smaller: ${asset}`);
    }
});



test('Smile Meter only preloads current neighbouring stages after intersection and decodes before commit', () => {
    const js = fs.readFileSync(JS_PATH, 'utf8');
    assert.match(js, /IntersectionObserver/);
    assert.match(js, /neighborAssets/);
    assert.match(js, /\.decode\(\)/);
    assert.match(js, /transitionToken/);
    assert.match(js, /requestAnimationFrame/);
    assert.doesNotMatch(js, /warmAssets/);
    assert.doesNotMatch(js, /requestIdleCallback/);
});

test('Smile Meter runtime stays local and does not require a network AI API', () => {
    const js = fs.readFileSync(JS_PATH, 'utf8');
    assert.doesNotMatch(js, /\bfetch\s*\(/);
    assert.doesNotMatch(js, /https?:\/\//);
    assert.match(js, /DentalClinicSmileMLEnhancer/);
});

test('all supported translation files remain valid JSON after Smile Meter changes', () => {
    for (const lang of ['ru', 'en', 'fr', 'el', 'ar']) {
        const filePath = path.join(ROOT, `wwwroot/assets/i18n/${lang}.json`);
        assert.doesNotThrow(() => JSON.parse(fs.readFileSync(filePath, 'utf8')), `${lang}.json is invalid`);
    }
});

test('view-specific visual focus prevents unsupported shape changes from substituting unrelated jaw images', () => {
    const html = fs.readFileSync(HTML_PATH, 'utf8');
    const js = fs.readFileSync(JS_PATH, 'utf8');
    assert.match(html, /id="smile-shape-view-note"/);
    assert.match(js, /const focusByView = \{/);
    assert.match(js, /VIEW_METRICS/);
    assert.match(js, /updateViewCapabilityHint/);
    assert.doesNotMatch(js, /resolveVisualFocus\(v, lastChanged, activePreset\)/);
});

test('preset animation updates controls continuously but commits the dental frame only after the animation finishes', () => {
    const js = fs.readFileSync(JS_PATH, 'utf8');
    assert.match(js, /updateStaticContent\(\{ suppressSwap: true \}\)/);
    assert.match(js, /else \{[\s\S]*?scheduleVisualUpdate\(\);/s);
    const frameBlock = js.match(/const frame = now => \{([\s\S]*?)\n        \};/)?.[1] || '';
    assert.doesNotMatch(frameBlock, /scheduleVisualUpdate\(\);[\s\S]*if \(p < 1\)/);
});


test('default Smile Meter slider values and placeholder image stay internally consistent on first paint', () => {
    const html = fs.readFileSync(HTML_PATH, 'utf8');
    const initialImage = html.match(/id="smile-model-image"[^>]*src="([^"]+)"/)?.[1];
    const whiteInput = html.match(/<input[^>]*id="slider-white"[^>]*>/)?.[0] || '';
    const alignInput = html.match(/<input[^>]*id="slider-align"[^>]*>/)?.[0] || '';
    const shapeInput = html.match(/<input[^>]*id="slider-shape"[^>]*>/)?.[0] || '';
    const white = Number(whiteInput.match(/value="(\d+)"/)?.[1]);
    const align = Number(alignInput.match(/value="(\d+)"/)?.[1]);
    const shape = Number(shapeInput.match(/value="(\d+)"/)?.[1]);
    const expectedAsset = selectViewAsset('front', { white, align, shape }, 'white', null);
    assert.equal(white, DEFAULT_VALUES.white);
    assert.equal(align, DEFAULT_VALUES.align);
    assert.equal(shape, DEFAULT_VALUES.shape);
    assert.equal(expectedAsset, defaultAssetForView('front'));
    assert.equal(initialImage, `/assets/images/Page_cosmetic-treatments/smile-meter/thumbs/${expectedAsset.replace(/\.png$/i, '.webp')}`);
});

test('manual input can cancel stale preset or AI animation loops instead of fighting the user', () => {
    const js = fs.readFileSync(JS_PATH, 'utf8');
    assert.match(js, /let animationToken = 0;/);
    assert.match(js, /function stopAnimation/);
    assert.match(js, /if \(token !== animationToken\) return;/);
    assert.match(js, /slider\.addEventListener\('input', \(\) => \{\s*stopAnimation\(\);/s);
});
