function cleanAddress(value) {
    return typeof value === 'string' && value.trim() ? value.trim() : null;
}

const ALLOWED_ROUTE_MODES = new Set(['driving', 'walking', 'transit', 'bicycling']);
const EMBED_DIRECTION_FLAGS = Object.freeze({
    driving: 'd',
    walking: 'w',
    transit: 'r',
    bicycling: 'b'
});

export function resolveClinicMapTarget(profile = {}) {
    if (profile.hasCoordinates
        && Number.isFinite(profile.latitude)
        && Number.isFinite(profile.longitude)) {
        return Object.freeze({
            type: 'coordinates',
            value: `${profile.latitude},${profile.longitude}`
        });
    }

    const address = cleanAddress(profile.address);
    return address ? Object.freeze({ type: 'address', value: address }) : null;
}

export function buildClinicMapEmbedUrl(target) {
    return target
        ? `https://www.google.com/maps?q=${encodeURIComponent(target.value)}&output=embed`
        : null;
}

// Full Google Maps link retained only as a compatibility helper for tests/legacy code.
// The contact page no longer opens it: routes are rendered in the existing map iframe.
export function buildClinicDirectionsUrl(target, origin = null, travelMode = 'driving') {
    if (!target) return null;

    const params = new URLSearchParams({
        api: '1',
        destination: target.value,
    });
    if (origin) params.set('origin', origin);
    params.set('travelmode', ALLOWED_ROUTE_MODES.has(travelMode) ? travelMode : 'driving');

    return `https://www.google.com/maps/dir/?${params.toString()}`;
}

/**
 * Build an embedded directions view that stays inside the clinic page.
 * Google Maps' classic embedded renderer understands saddr/daddr/dirflg and does not
 * require the user to leave the site. The destination always comes from trusted clinic
 * configuration; origin is either browser geolocation or text explicitly entered by user.
 */
export function buildClinicDirectionsEmbedUrl(target, origin, travelMode = 'driving') {
    if (!target || typeof origin !== 'string' || !origin.trim()) return null;

    const mode = ALLOWED_ROUTE_MODES.has(travelMode) ? travelMode : 'driving';
    const params = new URLSearchParams({
        output: 'embed',
        saddr: origin.trim(),
        daddr: target.value,
        dirflg: EMBED_DIRECTION_FLAGS[mode] || EMBED_DIRECTION_FLAGS.driving
    });

    return `https://www.google.com/maps?${params.toString()}`;
}

export function recommendTravelMode(distanceKmValue) {
    const km = Number(distanceKmValue);
    if (!Number.isFinite(km) || km < 0) return 'driving';
    if (km <= 2.2) return 'walking';
    if (km <= 12) return 'transit';
    return 'driving';
}

export function estimateTravelMinutes(distanceKmValue, mode = 'driving') {
    const km = Number(distanceKmValue);
    if (!Number.isFinite(km) || km < 0) return null;
    const speed = mode === 'walking' ? 4.8 : mode === 'transit' ? 22 : mode === 'bicycling' ? 14 : 32;
    return Math.max(mode === 'walking' ? 2 : 3, Math.round((km / speed) * 60));
}


/**
 * Lightweight local route prediction model used by the contact-page AI Route Studio.
 * This is intentionally privacy-preserving: it does not send the visitor's location to
 * an additional analytics/AI service. It combines distance, travel mode and time-of-day
 * into an approximate ETA + confidence value. Google Maps remains the source of the
 * actual route geometry shown in the embedded map.
 */
export function predictAdaptiveRoute(distanceKmValue, mode = 'driving', at = new Date()) {
    const km = Number(distanceKmValue);
    if (!Number.isFinite(km) || km < 0) return null;

    const safeMode = ALLOWED_ROUTE_MODES.has(mode) ? mode : 'driving';
    const baseline = estimateTravelMinutes(km, safeMode);
    const hour = at instanceof Date && !Number.isNaN(at.getTime()) ? at.getHours() : 12;
    const peak = (hour >= 7 && hour < 10) || (hour >= 16 && hour < 20);

    const timeFactor = safeMode === 'walking'
        ? 1
        : safeMode === 'transit'
            ? (peak ? 1.12 : 1.04)
            : safeMode === 'bicycling'
                ? 1.02
                : (peak ? 1.28 : 1.08);

    const overhead = safeMode === 'transit' ? 4 : safeMode === 'walking' ? 1 : 2;
    const minutes = Math.max(2, Math.round((baseline ?? 0) * timeFactor + overhead));
    const confidence = Math.max(72, Math.min(96, Math.round(94 - Math.min(km, 35) * 0.45 - (peak ? 3 : 0))));

    return Object.freeze({
        minutes,
        confidence,
        peak,
        model: 'adaptive-local-v1'
    });
}
