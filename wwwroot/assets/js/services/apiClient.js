const API_BASE = '/api';

function clearAuthSession() {
    for (const key of ['authToken', 'patientId', 'patientName', 'patientEmail', 'userRole']) {
        sessionStorage.removeItem(key);
    }
}

function isJwtExpired(token) {
    try {
        const [, payload] = token.split('.');
        if (!payload) return true;
        const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
        const decoded = JSON.parse(atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=')));
        return !decoded.exp || (decoded.exp * 1000) <= Date.now();
    } catch {
        return true;
    }
}

export async function apiFetch(endpoint, options = {}) {
    const url = `${API_BASE}${endpoint}`;
    let token = sessionStorage.getItem('authToken');

    if (token && isJwtExpired(token)) {
        clearAuthSession();
        token = null;
    }

    const headers = {
        'Content-Type': 'application/json',
        ...(options.headers || {})
    };
    if (token) headers.Authorization = `Bearer ${token}`;

    const config = {
        cache: 'no-store',
        credentials: 'same-origin',
        ...options,
        headers
    };

    const response = await fetch(url, config);
    if (response.status === 401 && token) {
        clearAuthSession();
        window.dispatchEvent(new CustomEvent('auth:expired'));
    }

    if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(error.message || `Ошибка ${response.status}`);
    }

    if (response.status === 204) return null;
    return await response.json();
}

export { clearAuthSession };
