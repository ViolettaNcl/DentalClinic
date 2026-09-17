/**
 * Contact page: AI Route Studio + live embedded route.
 *
 * Privacy boundary: the local prediction model never uploads location to an extra AI
 * provider. The actual route is rendered by Google Maps in the existing iframe only
 * after the visitor explicitly provides/permits a starting point.
 */
import { t, onLanguageChange } from '../../core/i18n.js';
import { getPublicClinicProfile } from '../../core/publicClinicProfile.js';
import {
    buildClinicDirectionsEmbedUrl,
    buildClinicMapEmbedUrl,
    estimateTravelMinutes,
    predictAdaptiveRoute,
    recommendTravelMode,
    resolveClinicMapTarget
} from '../../core/clinicMap.js';
import { runWhenDomReady } from '../../core/domReady.js';

const ROUTE_NUDGE_SESSION_KEY = 'dc_route_nudge_dismissed';

function distanceKm(lat1, lng1, lat2, lng2) {
    const R = 6371;
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) ** 2 +
        Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
        Math.sin(dLng / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function hydrateContactCards(profile) {
    document.querySelectorAll('.contact-cards-grid .contact-card').forEach(card => {
        const field = card.dataset.clinicField;
        const value = field && typeof profile[field] === 'string' ? profile[field] : null;
        const text = card.querySelector('.card__text');
        if (!text || !value) { card.hidden = true; return; }
        text.textContent = value;
        card.hidden = false;
    });
}

function prepareContactFactsForLoading() {
    document.querySelectorAll('.contact-cards-grid .contact-card').forEach(card => { card.hidden = true; });
    const location = document.querySelector('.location-section');
    if (location) location.hidden = true;
}

function setText(id, value, fallback = '—') {
    const node = document.getElementById(id);
    if (node) node.textContent = value || fallback;
}

function configureMap(profile) {
    const location = document.querySelector('.location-section');
    const iframe = location?.querySelector('.map-container iframe');
    const companion = document.getElementById('route-companion');
    const panel = document.getElementById('smart-map-panel');
    const target = resolveClinicMapTarget(profile);

    if (!target) {
        if (location) location.hidden = true;
        if (iframe) { iframe.removeAttribute('src'); iframe.hidden = true; }
        if (companion) companion.hidden = true;
        if (panel) panel.hidden = true;
        return null;
    }

    if (location) location.hidden = false;
    if (iframe) {
        iframe.src = buildClinicMapEmbedUrl(target);
        iframe.dataset.mapView = 'clinic';
        iframe.hidden = false;
        iframe.title = profile.address ? `Dental Clinic — ${profile.address}` : 'Dental Clinic location';
    }
    if (companion) companion.hidden = false;
    if (panel) panel.hidden = false;

    setText('smart-map-address', profile.address || target.value);
    setText('ai-map-phone', profile.phone);
    setText('ai-map-hours', profile.hours);
    setText('map-destination-label', profile.address || t('contact_map_title', 'Dental Clinic'));
    return target;
}

function renderRouteResult(resultEl, message, tone = 'neutral') {
    if (!resultEl) return;
    resultEl.textContent = message || '';
    resultEl.dataset.tone = tone;
    resultEl.classList.toggle('is-visible', Boolean(message));
}

function modeLabel(mode) {
    if (mode === 'walking') return t('smart_route_walking', 'Пешком').toLowerCase();
    if (mode === 'transit') return t('smart_route_transit', 'Транспорт').toLowerCase();
    return t('smart_route_driving', 'Машина').toLowerCase();
}

function initRouteBuilder(profile, target) {
    const iframe = document.querySelector('.location-section .map-container iframe');
    const mapZone = document.getElementById('clinic-map-interaction-zone');
    const companion = document.getElementById('route-companion');
    const toggle = document.getElementById('route-dock-toggle');
    const panel = document.getElementById('smart-map-panel');
    const panelClose = document.getElementById('route-panel-close');
    const nudge = document.getElementById('route-nudge');
    const nudgeAction = document.getElementById('route-nudge-action');
    const nudgeClose = document.getElementById('route-nudge-close');
    const resultEl = document.getElementById('route-result');
    const insight = document.getElementById('smart-map-insight');
    const originInput = document.getElementById('route-origin-input');
    const locationButton = document.getElementById('route-use-location');
    const buildButton = document.getElementById('route-build-on-map');
    const resetButton = document.getElementById('route-reset-map');
    const modeButtons = [...document.querySelectorAll('[data-route-mode]')];
    const scoreEl = document.getElementById('route-ai-score');
    const meterEl = document.getElementById('route-ai-meter');
    const etaEl = document.getElementById('map-route-eta');
    const distanceEl = document.getElementById('map-route-distance');
    const originStatusEl = document.getElementById('map-origin-status');

    if (!iframe || !mapZone || !companion || !panel || !target) return;

    const clinic = profile.hasCoordinates
        ? { lat: profile.latitude, lng: profile.longitude }
        : null;

    let selectedMode = 'driving';
    let currentOrigin = null;
    let currentDistance = null;
    let currentRouteOrigin = null;
    let nudgeTimer = null;
    let nudgeWasShown = false;

    const setPanelOpen = open => {
        companion.classList.toggle('is-open', open);
        toggle?.setAttribute('aria-expanded', String(open));
        panel.hidden = !open;
        if (open && window.matchMedia('(max-width: 820px)').matches) {
            requestAnimationFrame(() => originInput?.focus({ preventScroll: true }));
        }
    };

    const updatePredictionUi = () => {
        if (currentDistance === null) {
            if (scoreEl) scoreEl.textContent = '—';
            if (meterEl) meterEl.style.width = '18%';
            if (etaEl) etaEl.textContent = '—';
            if (distanceEl) distanceEl.textContent = '';
            return;
        }

        const prediction = predictAdaptiveRoute(currentDistance, selectedMode);
        if (!prediction) return;
        if (scoreEl) scoreEl.textContent = `${prediction.confidence}%`;
        if (meterEl) meterEl.style.width = `${prediction.confidence}%`;
        if (etaEl) etaEl.textContent = `≈ ${prediction.minutes} ${t('route_min_short', 'мин')}`;
        if (distanceEl) distanceEl.textContent = `${currentDistance.toFixed(1)} ${t('route_km_short', 'км')} · ${modeLabel(selectedMode)}`;
        if (insight) {
            insight.textContent = t('route_distance_live', '≈ {km} км · около {min} мин · {mode}')
                .replace('{km}', currentDistance.toFixed(1))
                .replace('{min}', String(prediction.minutes))
                .replace('{mode}', modeLabel(selectedMode));
        }
    };

    const hideNudge = (remember = false) => {
        clearTimeout(nudgeTimer);
        if (nudge) nudge.hidden = true;
        if (remember) {
            try { sessionStorage.setItem(ROUTE_NUDGE_SESSION_KEY, '1'); } catch { /* noop */ }
        }
    };

    const canShowNudge = () => {
        if (nudgeWasShown || !nudge || !window.matchMedia('(max-width: 820px)').matches) return false;
        try { return sessionStorage.getItem(ROUTE_NUDGE_SESSION_KEY) !== '1'; }
        catch { return true; }
    };

    const revealNudgeSoon = (delay = 950) => {
        if (!canShowNudge() || companion.classList.contains('is-open')) return;
        clearTimeout(nudgeTimer);
        nudgeTimer = window.setTimeout(() => {
            if (!canShowNudge() || companion.classList.contains('is-open')) return;
            nudgeWasShown = true;
            nudge.hidden = false;
        }, delay);
    };

    const renderRouteOnMap = origin => {
        const routeUrl = buildClinicDirectionsEmbedUrl(target, origin, selectedMode);
        if (!routeUrl) {
            renderRouteResult(resultEl, t('route_origin_required', 'Укажите стартовый адрес или разрешите геолокацию.'), 'warning');
            return false;
        }

        iframe.src = routeUrl;
        iframe.dataset.mapView = 'route';
        iframe.title = t('route_map_title', 'Маршрут до Dental Clinic');
        currentRouteOrigin = origin;
        mapZone.classList.add('has-route');
        if (originStatusEl) originStatusEl.textContent = originInput?.value?.trim() || t('route_origin_current', 'Моё местоположение');
        renderRouteResult(resultEl, t('route_rendered_here', 'Маршрут построен прямо на карте. Можно двигать и масштабировать её как обычно.'), 'success');
        return true;
    };

    const setMode = mode => {
        selectedMode = mode;
        modeButtons.forEach(button => button.classList.toggle('is-active', button.dataset.routeMode === mode));
        updatePredictionUi();
        if (currentRouteOrigin && iframe.dataset.mapView === 'route') renderRouteOnMap(currentRouteOrigin);
    };

    const setLocation = (latitude, longitude, buildImmediately = false) => {
        currentOrigin = `${latitude},${longitude}`;
        if (originInput) {
            originInput.value = t('route_origin_current', 'Моё местоположение');
            originInput.dataset.locationOrigin = currentOrigin;
        }
        if (originStatusEl) originStatusEl.textContent = t('route_origin_current', 'Моё местоположение');

        if (clinic) {
            currentDistance = distanceKm(latitude, longitude, clinic.lat, clinic.lng);
            const recommended = recommendTravelMode(currentDistance);
            selectedMode = recommended;
            modeButtons.forEach(button => button.classList.toggle('is-active', button.dataset.routeMode === recommended));
            updatePredictionUi();
        } else if (insight) {
            insight.textContent = t('route_location_ready', 'Местоположение определено. Выберите способ передвижения.');
        }

        if (buildImmediately) renderRouteOnMap(currentOrigin);
    };

    const requestLocation = (buildImmediately = false) => {
        if (!navigator.geolocation) {
            renderRouteResult(resultEl, t('route_no_geolocation', 'Геолокация не поддерживается вашим браузером.'), 'warning');
            originInput?.focus();
            return;
        }

        if (locationButton) locationButton.disabled = true;
        if (buildButton) buildButton.disabled = true;
        companion.classList.add('is-thinking');
        renderRouteResult(resultEl, t('route_locating', 'Определяем ваше местоположение…'));

        navigator.geolocation.getCurrentPosition(
            pos => {
                setLocation(pos.coords.latitude, pos.coords.longitude, buildImmediately);
                if (!buildImmediately) renderRouteResult(resultEl, t('route_location_ready', 'Местоположение определено. Выберите способ передвижения.'), 'success');
                if (locationButton) locationButton.disabled = false;
                if (buildButton) buildButton.disabled = false;
                companion.classList.remove('is-thinking');
            },
            err => {
                console.warn('Геолокация недоступна:', err.message);
                renderRouteResult(resultEl, t('route_geolocation_denied', 'Не удалось определить ваше местоположение — введите стартовый адрес вручную.'), 'warning');
                if (insight) insight.textContent = t('route_manual_hint', 'Можно ввести адрес отправления вручную — маршрут всё равно появится здесь.');
                if (locationButton) locationButton.disabled = false;
                if (buildButton) buildButton.disabled = false;
                companion.classList.remove('is-thinking');
                originInput?.focus();
            },
            { enableHighAccuracy: false, timeout: 9000, maximumAge: 300000 }
        );
    };

    modeButtons.forEach(button => button.addEventListener('click', () => setMode(button.dataset.routeMode || 'driving')));

    toggle?.addEventListener('click', () => {
        hideNudge(false);
        setPanelOpen(panel.hidden);
    });
    panelClose?.addEventListener('click', () => setPanelOpen(false));
    nudgeAction?.addEventListener('click', () => { hideNudge(false); setPanelOpen(true); });
    nudgeClose?.addEventListener('click', () => hideNudge(true));

    originInput?.addEventListener('input', () => {
        const stored = originInput.dataset.locationOrigin;
        if (!stored || originInput.value !== t('route_origin_current', 'Моё местоположение')) {
            currentOrigin = null;
            delete originInput.dataset.locationOrigin;
        }
        if (originStatusEl && originInput.value.trim()) originStatusEl.textContent = originInput.value.trim();
    });

    locationButton?.addEventListener('click', () => requestLocation(false));

    buildButton?.addEventListener('click', () => {
        const typedOrigin = originInput?.value?.trim() || '';
        const locationLabel = t('route_origin_current', 'Моё местоположение');
        const origin = currentOrigin || (typedOrigin && typedOrigin !== locationLabel ? typedOrigin : null);
        if (origin) { renderRouteOnMap(origin); return; }
        requestLocation(true);
    });

    resetButton?.addEventListener('click', () => {
        iframe.src = buildClinicMapEmbedUrl(target);
        iframe.dataset.mapView = 'clinic';
        iframe.title = profile.address ? `Dental Clinic — ${profile.address}` : 'Dental Clinic location';
        currentRouteOrigin = null;
        mapZone.classList.remove('has-route');
        renderRouteResult(resultEl, t('route_map_reset', 'Показываем расположение клиники.'), 'neutral');
    });

    // Desktop intentionally starts open like the premium reference layout. Mobile remains compact.
    setPanelOpen(!window.matchMedia('(max-width: 820px)').matches);
    updatePredictionUi();

    mapZone.addEventListener('pointerenter', () => revealNudgeSoon(800), { passive: true });
    mapZone.addEventListener('focusin', () => revealNudgeSoon(350));

    if ('IntersectionObserver' in window) {
        const observer = new IntersectionObserver(entries => {
            const visible = entries.some(entry => entry.isIntersecting && entry.intersectionRatio >= 0.55);
            if (visible && window.matchMedia('(pointer: coarse)').matches) revealNudgeSoon(1200);
        }, { threshold: [0.55] });
        observer.observe(mapZone);
    }

    if (typeof onLanguageChange === 'function') {
        onLanguageChange(() => {
            updatePredictionUi();
            if (originInput?.dataset.locationOrigin) originInput.value = t('route_origin_current', 'Моё местоположение');
        });
    }
}

async function initContactPage() {
    prepareContactFactsForLoading();
    try {
        const profile = await getPublicClinicProfile();
        hydrateContactCards(profile);
        const target = configureMap(profile);
        if (target) initRouteBuilder(profile, target);
    } catch (err) {
        console.warn('Не удалось загрузить публичные данные клиники:', err?.message || err);
        configureMap({ hasCoordinates: false });
    }
}

runWhenDomReady(initContactPage);
