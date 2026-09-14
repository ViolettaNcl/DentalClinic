function cleanAddress(value) {
    return typeof value === 'string' && value.trim() ? value.trim() : null;
}

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
    return address
        ? Object.freeze({ type: 'address', value: address })
        : null;
}

export function buildClinicMapEmbedUrl(target) {
    return target
        ? `https://www.google.com/maps?q=${encodeURIComponent(target.value)}&output=embed`
        : null;
}

export function buildClinicDirectionsUrl(target, origin = null) {
    if (!target) return null;

    const params = new URLSearchParams({
        api: '1',
        destination: target.value
    });
    if (origin) {
        params.set('origin', origin);
        params.set('travelmode', 'driving');
    }

    return `https://www.google.com/maps/dir/?${params.toString()}`;
}
