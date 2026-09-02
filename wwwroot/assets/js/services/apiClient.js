const API_BASE = '/api';

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
            const error = await response.json().catch(() => ({}));
            if (response.status === 401) {
                sessionStorage.removeItem('patientId');
                sessionStorage.removeItem('patientName');
                sessionStorage.removeItem('patientEmail');
                sessionStorage.removeItem('userRole');
                sessionStorage.removeItem('authToken'); // remove legacy tokens after upgrade
            }
            throw new Error(error.message || `Ошибка ${response.status}`);
        }

        if (response.status === 204) return null;
        return await response.json();
    } catch (error) {
        console.error('API request failed:', error?.message || 'unknown error');
        throw error;
    }
}
