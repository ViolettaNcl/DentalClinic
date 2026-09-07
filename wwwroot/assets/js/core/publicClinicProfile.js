let profilePromise;

function parseCoordinate(value, min, max) {
    if (value === null || value === undefined) return null;
    if (typeof value !== 'number' && typeof value !== 'string') return null;

    const candidate = typeof value === 'string' ? value.trim() : value;
    if (candidate === '') return null;

    const parsed = Number(candidate);
    return Number.isFinite(parsed) && parsed >= min && parsed <= max ? parsed : null;
}

function normalizeProfile(value) {
    const profile = value && typeof value === 'object' ? value : {};
    const latitude = parseCoordinate(profile.latitude, -90, 90);
    const longitude = parseCoordinate(profile.longitude, -180, 180);
    const hasCoordinates = latitude !== null && longitude !== null;

    return Object.freeze({
        phone: typeof profile.phone === 'string' && profile.phone.trim() ? profile.phone.trim() : null,
        email: typeof profile.email === 'string' && profile.email.trim() ? profile.email.trim() : null,
        address: typeof profile.address === 'string' && profile.address.trim() ? profile.address.trim() : null,
        hours: typeof profile.hours === 'string' && profile.hours.trim() ? profile.hours.trim() : null,
        latitude: hasCoordinates ? latitude : null,
        longitude: hasCoordinates ? longitude : null,
        hasCoordinates
    });
}

export function getPublicClinicProfile({ fetchImpl = fetch, force = false } = {}) {
    if (!force && profilePromise) return profilePromise;

    const request = fetchImpl('/api/clinic/profile', {
        method: 'GET',
        credentials: 'same-origin',
        headers: { Accept: 'application/json' }
    }).then(async response => {
        if (!response.ok) throw new Error(`clinic profile failed: ${response.status}`);
        return normalizeProfile(await response.json());
    });

    if (!force) {
        profilePromise = request.catch(error => {
            profilePromise = undefined;
            throw error;
        });
        return profilePromise;
    }

    return request;
}

export { normalizeProfile };
