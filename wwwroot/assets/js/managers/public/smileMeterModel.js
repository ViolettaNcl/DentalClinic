export const ASSET_BASE = '/assets/images/Page_cosmetic-treatments/smile-meter/';
export const THUMB_BASE = `${ASSET_BASE}thumbs/`;

export const PRESETS = Object.freeze({
    natural: { white: 52, align: 58, shape: 56 },
    balanced: { white: 76, align: 78, shape: 77 },
    hollywood: { white: 98, align: 95, shape: 94 }
});

/**
 * Metrics that can be represented by a real staged image for each camera view.
 * Shape is intentionally front-only: the available occlusal/side source renders
 * do not provide a trustworthy smile-arc progression, so those views must never
 * substitute an unrelated whitening/alignment image when the shape slider moves.
 */
export const VIEW_METRICS = Object.freeze({
    front: Object.freeze(['white', 'align', 'shape']),
    upper: Object.freeze(['white', 'align']),
    lower: Object.freeze(['white', 'align']),
    side: Object.freeze(['white', 'align'])
});

export const DEFAULT_VALUES = Object.freeze({ ...PRESETS.natural });

export const FRONT_SEQUENCES = Object.freeze({
    white: Object.freeze([
        'smile-3d-front-yellow-deep.png',
        'smile-3d-front-yellow.png',
        'smile-3d-front-yellow-medium.png',
        'smile-3d-front-cream.png',
        'smile-3d-front-balanced.png',
        'smile-3d-front-white.png',
        'smile-3d-front-very-white.png',
        'smile-3d-front-hollywood.png'
    ]),
    align: Object.freeze([
        'smile-3d-front-crowded.png',
        'smile-3d-front-crowded-medium.png',
        'smile-3d-front-crowded-soft.png',
        'smile-3d-front-balanced.png',
        'smile-3d-front-aligned.png',
        'smile-3d-front-ideal.png'
    ]),
    shape: Object.freeze([
        'smile-3d-front-shape-issue.png',
        'smile-3d-front-shape-low.png',
        'smile-3d-front-shape-medium.png',
        'smile-3d-front-shape-balanced.png',
        'smile-3d-front-shape-natural.png',
        'smile-3d-front-shape-ideal.png'
    ])
});

export const UPPER_SEQUENCES = Object.freeze({
    white: Object.freeze([
        'smile-3d-upper-yellow.png',
        'smile-3d-upper-warm-natural.png',
        'smile-3d-upper-yellow-medium.png',
        'smile-3d-upper-ideal.png',
        'smile-3d-upper-white.png'
    ]),
    align: Object.freeze([
        'smile-3d-upper-crowded.png',
        'smile-3d-upper-crowded-soft.png',
        'smile-3d-upper-aligned.png',
        'smile-3d-upper-ideal.png'
    ])
});

export const LOWER_SEQUENCES = Object.freeze({
    white: Object.freeze([
        'smile-3d-lower-yellow.png',
        'smile-3d-lower-ideal.png',
        'smile-3d-lower-white.png'
    ]),
    align: Object.freeze([
        'smile-3d-lower-crowded.png',
        'smile-3d-lower-crowded-soft.png',
        'smile-3d-lower-ideal.png'
    ])
});

export const SIDE_SEQUENCES = Object.freeze({
    white: Object.freeze([
        'smile-3d-side-yellow.png',
        'smile-3d-side-balanced.png',
        'smile-3d-side-white.png',
        'smile-3d-side-white-bright.png'
    ]),
    align: Object.freeze([
        'smile-3d-side-crowded-severe.png',
        'smile-3d-side-crowded-medium.png',
        'smile-3d-side-crowded-soft.png',
        'smile-3d-side-balanced.png',
        'smile-3d-side-ideal.png'
    ])
});

const SEQUENCES_BY_VIEW = Object.freeze({
    front: FRONT_SEQUENCES,
    upper: UPPER_SEQUENCES,
    lower: LOWER_SEQUENCES,
    side: SIDE_SEQUENCES
});

export const PRESET_ASSETS = Object.freeze({
    front: Object.freeze({
        natural: 'smile-3d-front-balanced.png',
        balanced: 'smile-3d-front-ideal.png',
        hollywood: 'smile-3d-front-hollywood.png'
    }),
    upper: Object.freeze({
        natural: 'smile-3d-upper-warm-natural.png',
        balanced: 'smile-3d-upper-ideal.png',
        hollywood: 'smile-3d-upper-white.png'
    }),
    lower: Object.freeze({
        natural: 'smile-3d-lower-ideal.png',
        balanced: 'smile-3d-lower-ideal.png',
        hollywood: 'smile-3d-lower-white.png'
    }),
    side: Object.freeze({
        natural: 'smile-3d-side-balanced.png',
        balanced: 'smile-3d-side-ideal.png',
        hollywood: 'smile-3d-side-white-bright.png'
    })
});

/**
 * Canonical camera-view metadata. Runtime sequences are validated against this
 * map so a top/bottom/side render cannot silently appear under another view.
 */
export const ASSET_VIEW = Object.freeze({
    // Front
    'smile-3d-front-yellow-deep.png': 'front',
    'smile-3d-front-yellow.png': 'front',
    'smile-3d-front-yellow-medium.png': 'front',
    'smile-3d-front-cream.png': 'front',
    'smile-3d-front-balanced.png': 'front',
    'smile-3d-front-white.png': 'front',
    'smile-3d-front-very-white.png': 'front',
    'smile-3d-front-hollywood.png': 'front',
    'smile-3d-front-crowded.png': 'front',
    'smile-3d-front-crowded-medium.png': 'front',
    'smile-3d-front-crowded-soft.png': 'front',
    'smile-3d-front-aligned.png': 'front',
    'smile-3d-front-ideal.png': 'front',
    'smile-3d-front-shape-issue.png': 'front',
    'smile-3d-front-shape-low.png': 'front',
    'smile-3d-front-shape-medium.png': 'front',
    'smile-3d-front-shape-balanced.png': 'front',
    'smile-3d-front-shape-natural.png': 'front',
    'smile-3d-front-shape-ideal.png': 'front',

    // Upper occlusal view
    'smile-3d-upper-yellow.png': 'upper',
    'smile-3d-upper-warm-natural.png': 'upper',
    'smile-3d-upper-yellow-medium.png': 'upper',
    'smile-3d-upper-crowded.png': 'upper',
    'smile-3d-upper-crowded-soft.png': 'upper',
    'smile-3d-upper-aligned.png': 'upper',
    'smile-3d-upper-ideal.png': 'upper',
    'smile-3d-upper-white.png': 'upper',

    // Lower occlusal view
    'smile-3d-lower-yellow.png': 'lower',
    'smile-3d-lower-crowded.png': 'lower',
    'smile-3d-lower-crowded-soft.png': 'lower',
    'smile-3d-lower-ideal.png': 'lower',
    'smile-3d-lower-white.png': 'lower',

    // Side / three-quarter view
    'smile-3d-side-yellow.png': 'side',
    'smile-3d-side-crowded-severe.png': 'side',
    'smile-3d-side-crowded-medium.png': 'side',
    'smile-3d-side-crowded-soft.png': 'side',
    'smile-3d-side-balanced.png': 'side',
    'smile-3d-side-ideal.png': 'side',
    'smile-3d-side-white.png': 'side',
    'smile-3d-side-white-bright.png': 'side'
});

export function clamp(value, min = 0, max = 100) {
    return Math.min(max, Math.max(min, Number(value) || 0));
}

export function easeOut(progress) {
    return 1 - Math.pow(1 - progress, 3);
}

export function aestheticScore(v) {
    const weighted = v.white * 0.34 + v.align * 0.38 + v.shape * 0.28;
    const spread = Math.max(v.white, v.align, v.shape) - Math.min(v.white, v.align, v.shape);
    const harmonyBonus = Math.max(0, 5 - spread * 0.06);
    return clamp(Math.round(weighted + harmonyBonus));
}

export function weakest(v, metrics = ['white', 'align', 'shape']) {
    return metrics
        .map(key => [key, Number(v[key])])
        .sort((a, b) => a[1] - b[1])[0][0];
}

export function metricSupported(view, metric) {
    return Boolean(VIEW_METRICS[view]?.includes(metric));
}

/** Map 0..100 to the nearest discrete frame index without playing intermediate frames. */
export function stageIndex(value, frameCount) {
    const count = Math.max(1, Number(frameCount) || 1);
    if (count === 1) return 0;
    const normalized = clamp(value) / 100;
    return Math.min(count - 1, Math.round(normalized * (count - 1)));
}

export function resolveVisualFocus(v, lastChanged = null, activePreset = null, view = 'front') {
    const supported = VIEW_METRICS[view] || VIEW_METRICS.front;
    if (activePreset) return 'white';
    if (lastChanged && supported.includes(lastChanged)) return lastChanged;
    return weakest(v, supported);
}

export function getSequenceForState(view, focus) {
    const sequences = SEQUENCES_BY_VIEW[view] || FRONT_SEQUENCES;
    const normalizedFocus = sequences[focus] ? focus : 'white';
    return sequences[normalizedFocus] || [];
}

export function selectViewAsset(view, v, focus = null, activePreset = null) {
    const normalizedView = VIEW_METRICS[view] ? view : 'front';
    const presetAsset = activePreset ? PRESET_ASSETS[normalizedView]?.[activePreset] : null;
    if (presetAsset) return presetAsset;

    const normalizedFocus = metricSupported(normalizedView, focus)
        ? focus
        : resolveVisualFocus(v, focus, null, normalizedView);
    const sequence = getSequenceForState(normalizedView, normalizedFocus);
    if (!sequence.length) return PRESET_ASSETS[normalizedView].natural;
    return sequence[stageIndex(v[normalizedFocus], sequence.length)];
}

export function selectFrontAsset(v, lastChanged = null, activePreset = null) {
    return selectViewAsset('front', v, lastChanged, activePreset);
}

export function thumbAsset(view, v, focus = null, activePreset = null) {
    return selectViewAsset(view, v, focus, activePreset);
}

export function thumbPath(asset) {
    return `${THUMB_BASE}${asset.replace(/\.png$/i, '.webp')}`;
}

export function runtimePath(asset) {
    return `${ASSET_BASE}runtime/${asset.replace(/\.png$/i, '.webp')}`;
}

export function pngPath(asset) {
    return `${ASSET_BASE}${asset}`;
}

export function neighborAssets(view, focus, asset) {
    const sequence = getSequenceForState(view, focus);
    const index = sequence.indexOf(asset);
    if (index < 0) return [asset].filter(Boolean);
    return [sequence[index - 1], sequence[index], sequence[index + 1]].filter(Boolean);
}

export function getColorGrade() {
    return { sepia: 0, hue: '0deg', saturation: 1, brightness: 1, contrast: 1 };
}

export function buildTransitionSequence({ toAsset }) {
    return [toAsset].filter(Boolean);
}

export function validateSmileAssetCatalog() {
    const issues = [];
    for (const [view, sequences] of Object.entries(SEQUENCES_BY_VIEW)) {
        for (const [metric, assets] of Object.entries(sequences)) {
            if (!VIEW_METRICS[view]?.includes(metric)) {
                issues.push(`${view}/${metric}: unsupported metric sequence`);
            }
            for (const asset of assets) {
                if (ASSET_VIEW[asset] !== view) {
                    issues.push(`${view}/${metric}: ${asset} classified as ${ASSET_VIEW[asset] || 'unknown'}`);
                }
            }
        }
    }
    for (const [view, presets] of Object.entries(PRESET_ASSETS)) {
        for (const [preset, asset] of Object.entries(presets)) {
            if (ASSET_VIEW[asset] !== view) {
                issues.push(`${view}/${preset} preset: ${asset} classified as ${ASSET_VIEW[asset] || 'unknown'}`);
            }
        }
    }
    return issues;
}

export function defaultAssetForView(view = 'front') {
    return selectViewAsset(view, DEFAULT_VALUES, 'white', null);
}
