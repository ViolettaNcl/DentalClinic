let profilePromise;

function normalizeProfile(value) {
    const profile = value && typeof value === 'object' ? value : {};
    const latitude = Number(profile.latitude);
    const longitude = Number(profile.longitude);
    const hasCoordinates = Number.isFinite(latitude)
        && latitude >= -90 && latitude <= 90
        && Number.isFinite(longitude)
        && longitude >= -180 && longitude <= 180;

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
