import test from 'node:test';
import assert from 'node:assert/strict';
import {
    aestheticScore,
    resolveVisualFocus,
    selectFrontAsset,
    selectViewAsset,
    buildTransitionSequence,
    PRESETS,
    getColorGrade,
    metricSupported,
    validateSmileAssetCatalog
} from '../../wwwroot/assets/js/managers/public/smileMeterModel.js';

test('aestheticScore is bounded and rewards harmony', () => {
    assert.equal(aestheticScore({ white: 100, align: 100, shape: 100 }), 100);
    assert.equal(aestheticScore({ white: 0, align: 0, shape: 0 }), 5);
    assert.ok(aestheticScore({ white: 72, align: 72, shape: 72 }) > aestheticScore({ white: 72, align: 10, shape: 72 }));
});

test('visual focus follows the changed metric only when that camera view can represent it', () => {
    const v = { white: 70, align: 12, shape: 4 };
    assert.equal(resolveVisualFocus(v, 'shape', null, 'front'), 'shape');
    assert.equal(resolveVisualFocus(v, 'shape', null, 'upper'), 'align');
    assert.equal(resolveVisualFocus(v, 'align', null, 'lower'), 'align');
    assert.equal(metricSupported('side', 'shape'), false);
    assert.equal(metricSupported('front', 'shape'), true);
});

test('front whitening has a stable eight-stage progression', () => {
    assert.equal(selectFrontAsset({ white: 6, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-yellow-deep.png');
    assert.equal(selectFrontAsset({ white: 18, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-yellow.png');
    assert.equal(selectFrontAsset({ white: 30, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-yellow-medium.png');
    assert.equal(selectFrontAsset({ white: 42, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-cream.png');
    assert.equal(selectFrontAsset({ white: 56, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-balanced.png');
    assert.equal(selectFrontAsset({ white: 70, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-white.png');
    assert.equal(selectFrontAsset({ white: 82, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-very-white.png');
    assert.equal(selectFrontAsset({ white: 96, align: 70, shape: 70 }, 'white', null), 'smile-3d-front-hollywood.png');
});

test('front alignment and shape sliders map across multiple distinct same-view states', () => {
    const alignAssets = [0, 20, 40, 60, 80, 100].map(v => selectFrontAsset({ white: 70, align: v, shape: 70 }, 'align', null));
    const shapeAssets = [0, 20, 40, 60, 80, 100].map(v => selectFrontAsset({ white: 70, align: 70, shape: v }, 'shape', null));
    assert.equal(new Set(alignAssets).size, 6, `alignment stages: ${alignAssets.join(', ')}`);
    assert.equal(new Set(shapeAssets).size, 6, `shape stages: ${shapeAssets.join(', ')}`);
    assert.ok(alignAssets.every(asset => asset.includes('-front-')));
    assert.ok(shapeAssets.every(asset => asset.includes('-front-')));
});

test('upper view never substitutes side or lower-jaw photography', () => {
    assert.equal(selectViewAsset('upper', { white: 0, align: 60, shape: 60 }, 'white', null), 'smile-3d-upper-yellow.png');
    assert.equal(selectViewAsset('upper', { white: 34, align: 60, shape: 60 }, 'white', null), 'smile-3d-upper-warm-natural.png');
    assert.equal(selectViewAsset('upper', { white: 62, align: 60, shape: 60 }, 'white', null), 'smile-3d-upper-yellow-medium.png');
    assert.equal(selectViewAsset('upper', { white: 100, align: 60, shape: 60 }, 'white', null), 'smile-3d-upper-white.png');
    assert.equal(selectViewAsset('upper', { white: 60, align: 0, shape: 60 }, 'align', null), 'smile-3d-upper-crowded.png');
    assert.equal(selectViewAsset('upper', { white: 60, align: 68, shape: 60 }, 'align', null), 'smile-3d-upper-aligned.png');
});

test('lower view stays lower-jaw-only and uses honest three-stage alignment', () => {
    assert.equal(selectViewAsset('lower', { white: 0, align: 60, shape: 60 }, 'white', null), 'smile-3d-lower-yellow.png');
    assert.equal(selectViewAsset('lower', { white: 50, align: 60, shape: 60 }, 'white', null), 'smile-3d-lower-ideal.png');
    assert.equal(selectViewAsset('lower', { white: 100, align: 60, shape: 60 }, 'white', null), 'smile-3d-lower-white.png');
    assert.equal(selectViewAsset('lower', { white: 60, align: 0, shape: 60 }, 'align', null), 'smile-3d-lower-crowded.png');
    assert.equal(selectViewAsset('lower', { white: 60, align: 50, shape: 60 }, 'align', null), 'smile-3d-lower-crowded-soft.png');
    assert.equal(selectViewAsset('lower', { white: 60, align: 100, shape: 60 }, 'align', null), 'smile-3d-lower-ideal.png');
});

test('side view uses only side/three-quarter renders across whitening and crowding', () => {
    assert.equal(selectViewAsset('side', { white: 0, align: 80, shape: 80 }, 'white', null), 'smile-3d-side-yellow.png');
    assert.equal(selectViewAsset('side', { white: 100, align: 80, shape: 80 }, 'white', null), 'smile-3d-side-white-bright.png');
    assert.equal(selectViewAsset('side', { white: 80, align: 0, shape: 80 }, 'align', null), 'smile-3d-side-crowded-severe.png');
    assert.equal(selectViewAsset('side', { white: 80, align: 25, shape: 80 }, 'align', null), 'smile-3d-side-crowded-medium.png');
    assert.equal(selectViewAsset('side', { white: 80, align: 50, shape: 80 }, 'align', null), 'smile-3d-side-crowded-soft.png');
    assert.equal(selectViewAsset('side', { white: 80, align: 100, shape: 80 }, 'align', null), 'smile-3d-side-ideal.png');
});

test('presets map to stable view-specific assets', () => {
    assert.equal(selectFrontAsset(PRESETS.natural, 'white', 'natural'), 'smile-3d-front-balanced.png');
    assert.equal(selectFrontAsset(PRESETS.balanced, 'white', 'balanced'), 'smile-3d-front-ideal.png');
    assert.equal(selectFrontAsset(PRESETS.hollywood, 'white', 'hollywood'), 'smile-3d-front-hollywood.png');
    assert.equal(selectViewAsset('upper', PRESETS.hollywood, 'white', 'hollywood'), 'smile-3d-upper-white.png');
    assert.equal(selectViewAsset('lower', PRESETS.hollywood, 'white', 'hollywood'), 'smile-3d-lower-white.png');
    assert.equal(selectViewAsset('side', PRESETS.hollywood, 'white', 'hollywood'), 'smile-3d-side-white-bright.png');
});

test('manual transitions commit only the target staged frame without intermediate playback', () => {
    assert.deepEqual(buildTransitionSequence({ view: 'front', focus: 'white', fromAsset: 'smile-3d-front-yellow-deep.png', toAsset: 'smile-3d-front-white.png' }), ['smile-3d-front-white.png']);
    assert.deepEqual(buildTransitionSequence({ view: 'upper', focus: 'align', fromAsset: 'smile-3d-upper-ideal.png', toAsset: 'smile-3d-upper-crowded.png' }), ['smile-3d-upper-crowded.png']);
    assert.deepEqual(buildTransitionSequence({ view: 'side', focus: 'white', fromAsset: 'smile-3d-side-yellow.png', toAsset: 'smile-3d-side-white-bright.png' }), ['smile-3d-side-white-bright.png']);
});

test('asset catalog validates with no cross-view assignments', () => {
    assert.deepEqual(validateSmileAssetCatalog(), []);
});

test('color grading remains neutral so whitening never recolors gums globally', () => {
    assert.deepEqual(getColorGrade(), { sepia: 0, hue: '0deg', saturation: 1, brightness: 1, contrast: 1 });
});
