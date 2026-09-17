/**
 * Premium Smile Meter
 * Stable single-layer renderer with deterministic multi-stage transitions.
 * Runtime guarantee: one visible dental image only; no layered dental compositing.
 * This is an aesthetic simulator, not medical diagnostics.
 */
import { t, onLanguageChange, ready } from '../../core/i18n.js';
import {
    ASSET_BASE,
    PRESETS,
    clamp,
    easeOut,
    aestheticScore,
    weakest,
    VIEW_METRICS,
    DEFAULT_VALUES,
    selectViewAsset,
    thumbAsset,
    thumbPath,
    getColorGrade,
    buildTransitionSequence,
    runtimePath,
    pngPath,
    neighborAssets
} from './smileMeterModel.js';

(async function initSmileMeter() {
    await ready;

    const root = document.getElementById('smile-meter');
    if (!root) return;

    const stage = document.getElementById('smile-3d-stage');
    const shell = document.getElementById('smile-3d-model');
    const modelImage = document.getElementById('smile-model-image');
    const sliderWhite = document.getElementById('slider-white');
    const sliderAlign = document.getElementById('slider-align');
    const sliderShape = document.getElementById('slider-shape');
    const scoreValueEl = document.getElementById('smile-score-value');
    const scoreRing = document.getElementById('smile-score-ring');
    const resultText = document.getElementById('smile-result-text');
    const suggestions = document.getElementById('smile-ai-suggestions');
    const autoScanBtn = document.getElementById('smile-auto-scan');
    const viewButtons = [...root.querySelectorAll('[data-smile-view]')];
    const presetButtons = [...root.querySelectorAll('[data-smile-preset]')];
    const shapeViewNote = document.getElementById('smile-shape-view-note');
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (!stage || !shell || !modelImage || !sliderWhite || !sliderAlign || !sliderShape) return;

    const COLORS = { white: '#12b8a2', align: '#ff9a4a', shape: '#27b8e6' };
    let currentView = 'front';
    let activePreset = null;
    let lastChanged = 'white';
    const focusByView = {
        front: 'white',
        upper: 'white',
        lower: 'white',
        side: 'white'
    };
    let currentAsset = null;
    let runtimeReady = false;
    let scoreDisplayed = 0;
    let scoreRaf = 0;
    let transitionToken = 0;
    let visualTimer = 0;
    let animationToken = 0;
    let animationRaf = 0;

    const thumbEls = {
        front: document.getElementById('smile-thumb-front'),
        upper: document.getElementById('smile-thumb-upper'),
        lower: document.getElementById('smile-thumb-lower'),
        side: document.getElementById('smile-thumb-side')
    };

    const values = () => ({
        white: Number(sliderWhite.value),
        align: Number(sliderAlign.value),
        shape: Number(sliderShape.value)
    });

    const decodedCache = new Map();
    let smileVisible = false;

    function desiredAssetFor(view = currentView) {
        const v = values();
        const focus = focusByView[view] || 'white';
        return selectViewAsset(view, v, focus, activePreset);
    }

    function syncPreviewAsset(view = currentView) {
        const asset = desiredAssetFor(view);
        if (!asset) return null;
        modelImage.src = thumbPath(asset);
        modelImage.dataset.asset = asset;
        currentAsset = asset;
        return asset;
    }

    function stopAnimation({ clearBusy = true } = {}) {
        animationToken += 1;
        if (animationRaf) cancelAnimationFrame(animationRaf);
        animationRaf = 0;
        root.classList.remove('is-ai-scanning');
        if (clearBusy) autoScanBtn?.removeAttribute('aria-busy');
    }

    async function decodeAsset(asset) {
        if (!asset) return null;
        if (decodedCache.has(asset)) return decodedCache.get(asset);
        const promise = (async () => {
            const img = new Image();
            img.decoding = 'async';
            img.src = runtimePath(asset);
            try {
                if (typeof img.decode === 'function') await img.decode();
                else await new Promise((resolve, reject) => { img.onload = resolve; img.onerror = reject; });
                return img.src;
            } catch {
                const fallback = new Image();
                fallback.decoding = 'async';
                fallback.src = pngPath(asset);
                try {
                    if (typeof fallback.decode === 'function') await fallback.decode();
                    else await new Promise((resolve, reject) => { fallback.onload = resolve; fallback.onerror = reject; });
                    return fallback.src;
                } catch { return null; }
            }
        })();
        decodedCache.set(asset, promise);
        return promise;
    }

    function warmNeighbors(view, focus, asset) {
        if (!smileVisible) return;
        neighborAssets(view, focus, asset).forEach(item => { void decodeAsset(item); });
    }

    async function showAsset(asset, token) {
        if (!asset || token !== transitionToken || (asset === currentAsset && runtimeReady)) return;
        const src = await decodeAsset(asset);
        if (!src || token !== transitionToken) return;
        await new Promise(resolve => requestAnimationFrame(resolve));
        if (token !== transitionToken) return;
        modelImage.src = src;
        modelImage.dataset.asset = asset;
        currentAsset = asset;
        runtimeReady = true;
    }

    async function runTransition(targetAsset, focus) {
        const token = ++transitionToken;
        const [asset] = buildTransitionSequence({ view: currentView, focus, fromAsset: currentAsset, toAsset: targetAsset });
        await showAsset(asset, token);
        if (token === transitionToken) warmNeighbors(currentView, focus, targetAsset);
    }

    function applyColorGrade() {
        const grade = getColorGrade();
        shell.style.setProperty('--smile-sepia', String(grade.sepia));
        shell.style.setProperty('--smile-hue', grade.hue);
        shell.style.setProperty('--smile-saturation', String(grade.saturation));
        shell.style.setProperty('--smile-brightness', String(grade.brightness));
        shell.style.setProperty('--smile-contrast', String(grade.contrast));
    }

    function updateThumbnails(v) {
        Object.entries(thumbEls).forEach(([view, el]) => {
            if (!el) return;
            const focus = focusByView[view] || 'white';
            el.src = thumbPath(thumbAsset(view, v, focus, activePreset));
            el.dataset.asset = thumbAsset(view, v, focus, activePreset);
        });
    }

    function updateSliders(v) {
        [
            [sliderWhite, 'white', v.white],
            [sliderAlign, 'align', v.align],
            [sliderShape, 'shape', v.shape]
        ].forEach(([slider, key, value]) => {
            slider.style.setProperty('--val', `${value}%`);
            slider.style.setProperty('--slider-color', COLORS[key]);
            const label = slider.closest('.smile-metric-card')?.querySelector('.smile-slider-value');
            if (label) label.textContent = `${value}%`;
        });
    }

    function animateScore(target) {
        if (scoreRaf) cancelAnimationFrame(scoreRaf);
        const apply = v => {
            scoreDisplayed = v;
            if (scoreValueEl) scoreValueEl.textContent = String(v);
            if (scoreRing) scoreRing.style.setProperty('--score', String(v));
        };
        if (reduceMotion) return apply(target);
        const from = scoreDisplayed;
        const start = performance.now();
        const duration = 280;
        const tick = now => {
            const p = Math.min(1, (now - start) / duration);
            apply(Math.round(from + (target - from) * easeOut(p)));
            if (p < 1) scoreRaf = requestAnimationFrame(tick);
        };
        scoreRaf = requestAnimationFrame(tick);
    }

    function analysisText(v, score) {
        if (score >= 92) return t('smile_analysis_great', 'Параметры выглядят эстетически гармонично. Симулятор показывает яркий, ровный и пропорциональный вариант улыбки.');
        const key = weakest(v);
        if (key === 'white') return t('smile_analysis_white', 'Главный визуальный потенциал сейчас связан с оттенком эмали. Более светлый тон заметно изменит общее впечатление от улыбки.');
        if (key === 'align') return t('smile_analysis_align', 'Главный визуальный потенциал сейчас связан с ровностью зубного ряда. Симулятор показывает, как выравнивание меняет общую гармонию улыбки.');
        return t('smile_analysis_shape', 'Главный визуальный потенциал сейчас связан с пропорциями и линией улыбки. Более гармоничная форма делает результат визуально сбалансированнее.');
    }

    function recommendations(v, score) {
        const items = [];
        for (const [key, value] of Object.entries(v).sort((a, b) => a[1] - b[1])) {
            if (items.length >= 3 || value >= 92) continue;
            if (key === 'white') items.push(t('smile_suggest_white', 'Профессиональная гигиена и оценка оттенка эмали.'));
            if (key === 'align') items.push(t('smile_suggest_align', 'Оценка возможностей эстетической коррекции положения зубов.'));
            if (key === 'shape') items.push(t('smile_suggest_shape', 'Цифровое моделирование оптимальных пропорций улыбки.'));
        }
        if (score >= 92) items.unshift(t('smile_suggest_great', 'Сохранить естественность результата и обсудить только деликатные эстетические изменения.'));
        while (items.length < 3) items.push(t('smile_suggest_maintain', 'Поддерживать результат регулярной профессиональной гигиеной и профилактическими осмотрами.'));
        return items.slice(0, 3);
    }

    function renderRecommendations(items) {
        if (!suggestions) return;
        suggestions.replaceChildren(...items.map(text => {
            const li = document.createElement('li');
            li.textContent = text;
            return li;
        }));
    }

    function calloutText(key, value) {
        if (key === 'white') {
            if (value < 18) return t('smile_callout_white_low', 'Выраженный тёплый оттенок');
            if (value < 45) return t('smile_callout_white_mid', 'Натуральный тёплый оттенок');
            if (value < 72) return t('smile_callout_white_balanced', 'Сбалансированный натуральный оттенок');
            return t('smile_callout_white_high', 'Светлый эстетичный оттенок');
        }
        if (key === 'align') {
            if (value < 18) return t('smile_callout_align_low', 'Заметная визуальная скученность');
            if (value < 45) return t('smile_callout_align_mid', 'Умеренная неровность зубного ряда');
            if (value < 72) return t('smile_callout_align_balanced', 'Небольшая неровность зубного ряда');
            return t('smile_callout_align_high', 'Хорошая визуальная ровность');
        }
        if (value < 18) return t('smile_callout_shape_low', 'Асимметрия и неровная линия улыбки');
        if (value < 45) return t('smile_callout_shape_mid', 'Натуральная форма с лёгкой асимметрией');
        if (value < 72) return t('smile_callout_shape_balanced', 'Естественные пропорции');
        return t('smile_callout_shape_high', 'Гармоничные пропорции');
    }

    function updateCallouts(v) {
        const white = document.getElementById('smile-callout-white-text');
        const align = document.getElementById('smile-callout-align-text');
        const shape = document.getElementById('smile-callout-shape-text');
        if (white) white.textContent = calloutText('white', v.white);
        if (align) align.textContent = calloutText('align', v.align);
        if (shape) shape.textContent = calloutText('shape', v.shape);
    }

    function updateStaticContent({ languageOnly = false, suppressSwap = false } = {}) {
        const v = values();
        const score = aestheticScore(v);
        updateSliders(v);
        animateScore(score);
        updateCallouts(v);
        updateThumbnails(v);
        applyColorGrade();
        if (resultText) {
            resultText.textContent = analysisText(v, score);
            if (!languageOnly && !suppressSwap && !reduceMotion) {
                resultText.classList.remove('swap');
                void resultText.offsetWidth;
                resultText.classList.add('swap');
            }
        }
        renderRecommendations(recommendations(v, score));
    }

    function scheduleVisualUpdate() {
        clearTimeout(visualTimer);
        if (!smileVisible) return;
        visualTimer = window.setTimeout(() => requestAnimationFrame(() => {
            const v = values();
            const focus = focusByView[currentView] || 'white';
            const targetAsset = selectViewAsset(currentView, v, focus, activePreset);
            void runTransition(targetAsset, focus);
        }), reduceMotion ? 0 : 36);
    }

    function updateDynamicLanguage() {
        modelImage.alt = t('smile_model_alt', 'Реалистичная 3D-модель зубов и дёсен для эстетической симуляции улыбки');
        sliderWhite.setAttribute('aria-label', t('smile_slider_white', 'Белизна зубов'));
        sliderAlign.setAttribute('aria-label', t('smile_slider_align', 'Ровность зубов'));
        sliderShape.setAttribute('aria-label', t('smile_slider_shape', 'Форма улыбки'));
    }

    function renderNow({ languageOnly = false, suppressSwap = false, deferVisual = false } = {}) {
        updateStaticContent({ languageOnly, suppressSwap });
        if (!smileVisible) syncPreviewAsset(currentView);
        if (!deferVisual) scheduleVisualUpdate();
    }

    function updateViewCapabilityHint() {
        if (!shapeViewNote) return;
        shapeViewNote.hidden = VIEW_METRICS[currentView]?.includes('shape') ?? false;
    }

    function setView(view) {
        currentView = ['front', 'upper', 'lower', 'side'].includes(view) ? view : 'front';
        stage.dataset.view = currentView;
        viewButtons.forEach(button => {
            const active = button.dataset.smileView === currentView;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', String(active));
        });
        updateViewCapabilityHint();
        if (!smileVisible || !runtimeReady) syncPreviewAsset(currentView);
        renderNow();
    }

    function setActivePreset(name) {
        activePreset = name;
        presetButtons.forEach(button => {
            button.classList.toggle('is-active', button.dataset.smilePreset === name);
        });
    }

    function animateTo(target, presetName = null) {
        stopAnimation({ clearBusy: false });
        const token = ++animationToken;
        const from = values();
        const duration = reduceMotion ? 0 : 480;
        const start = performance.now();
        const showScan = presetName && !reduceMotion;
        if (showScan) root.classList.add('is-ai-scanning');
        autoScanBtn?.setAttribute('aria-busy', 'true');
        setActivePreset(presetName);
        if (presetName) {
            lastChanged = 'white';
            for (const view of Object.keys(focusByView)) focusByView[view] = 'white';
        }

        const frame = now => {
            if (token !== animationToken) return;
            const p = duration === 0 ? 1 : Math.min(1, (now - start) / duration);
            const e = easeOut(p);
            sliderWhite.value = String(Math.round(from.white + (target.white - from.white) * e));
            sliderAlign.value = String(Math.round(from.align + (target.align - from.align) * e));
            sliderShape.value = String(Math.round(from.shape + (target.shape - from.shape) * e));
            updateStaticContent({ suppressSwap: true });
            if (!smileVisible) syncPreviewAsset(currentView);
            if (p < 1) animationRaf = requestAnimationFrame(frame);
            else {
                animationRaf = 0;
                scheduleVisualUpdate();
                if (showScan) root.classList.remove('is-ai-scanning');
                autoScanBtn?.removeAttribute('aria-busy');
            }
        };
        animationRaf = requestAnimationFrame(frame);
    }

    async function optionalEnhancement(input, fallbackTarget) {
        const enhancer = window.DentalClinicSmileMLEnhancer;
        if (typeof enhancer !== 'function') return fallbackTarget;
        try {
            const result = await Promise.race([
                Promise.resolve(enhancer({ ...input })),
                new Promise((_, reject) => setTimeout(() => reject(new Error('Smile ML timeout')), 800))
            ]);
            if (!result || typeof result !== 'object') return fallbackTarget;
            return {
                white: clamp(Number(result.white ?? fallbackTarget.white)),
                align: clamp(Number(result.align ?? fallbackTarget.align)),
                shape: clamp(Number(result.shape ?? fallbackTarget.shape))
            };
        } catch {
            return fallbackTarget;
        }
    }

    [[sliderWhite, 'white'], [sliderAlign, 'align'], [sliderShape, 'shape']].forEach(([slider, key]) => {
        slider.addEventListener('input', () => {
            stopAnimation();
            lastChanged = key;
            setActivePreset(null);
            for (const [view, supported] of Object.entries(VIEW_METRICS)) {
                if (supported.includes(key)) focusByView[view] = key;
            }
            renderNow();
        });
    });

    viewButtons.forEach(button => button.addEventListener('click', () => {
        stopAnimation();
        setView(button.dataset.smileView);
    }));
    presetButtons.forEach(button => button.addEventListener('click', () => animateTo(PRESETS[button.dataset.smilePreset] || PRESETS.balanced, button.dataset.smilePreset)));

    autoScanBtn?.addEventListener('click', async () => {
        const v = values();
        const deterministicTarget = {
            white: Math.min(94, Math.round(v.white * 0.42 + 56)),
            align: Math.min(93, Math.round(v.align * 0.40 + 57)),
            shape: Math.min(92, Math.round(v.shape * 0.42 + 55))
        };
        lastChanged = weakest(v);
        for (const [view, supported] of Object.entries(VIEW_METRICS)) {
            if (supported.includes(lastChanged)) focusByView[view] = lastChanged;
        }
        animateTo(await optionalEnhancement(v, deterministicTarget), null);
    });

    const observer = new IntersectionObserver(entries => {
        if (!entries.some(entry => entry.isIntersecting)) return;
        smileVisible = true;
        observer.disconnect();
        const focus = focusByView[currentView] || 'white';
        const asset = desiredAssetFor(currentView);
        void runTransition(asset, focus);
        warmNeighbors(currentView, focus, asset);
    }, { rootMargin: '300px 0px' });
    observer.observe(root);

    if (typeof onLanguageChange === 'function') {
        onLanguageChange(() => {
            updateDynamicLanguage();
            renderNow({ languageOnly: true });
        });
    }

    sliderWhite.value = sliderWhite.value || String(DEFAULT_VALUES.white);
    sliderAlign.value = sliderAlign.value || String(DEFAULT_VALUES.align);
    sliderShape.value = sliderShape.value || String(DEFAULT_VALUES.shape);
    syncPreviewAsset('front');
    updateDynamicLanguage();
    setView('front');
})();
