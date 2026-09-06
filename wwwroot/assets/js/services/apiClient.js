const API_BASE = '/api';

export class ApiError extends Error {
    constructor(message, status, payload = null) {
        super(message);
        this.name = 'ApiError';
        this.status = Number.isInteger(status) ? status : null;
        this.payload = payload;
    }
}

function clearLocalSessionMetadata() {
    if (typeof sessionStorage === 'undefined') return;

    ['patientId', 'patientName', 'patientEmail', 'userRole', 'authToken']
        .forEach(key => sessionStorage.removeItem(key));
}

export async function apiFetch(endpoint, options = {}) {
    const url = `${API_BASE}${endpoint}`;

    const headers = {
        'Content-Type': 'application/json',
        ...(options.headers || {})
    };

    const config = {
        ...options,
        headers,
        // The JWT is stored in an HttpOnly SameSite cookie and is therefore never
        // exposed to JavaScript/sessionStorage. Same-origin fetch sends it automatically.
        credentials: options.credentials || 'same-origin'
    };

    try {
        const response = await fetch(url, config);
        if (!response.ok) {
            const payload = await response.json().catch(() => ({}));
            if (response.status === 401)
                clearLocalSessionMetadata();

            // Preserve HTTP status for callers that need to distinguish an
            // authentication failure from rate limits, server failures or a bad request.
            // This is particularly important for the shared patient/admin login form.
            throw new ApiError(payload.message || `Ошибка ${response.status}`, response.status, payload);
        }

        if (response.status === 204) return null;
        return await response.json();
    } catch (error) {
        console.error('API request failed:', error?.message || 'unknown error');
        throw error;
    }
}
