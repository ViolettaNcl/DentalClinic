const API_BASE = '/api';

export async function apiFetch(endpoint, options = {}) {
    const url = `${API_BASE}${endpoint}`;
    const token = sessionStorage.getItem('authToken');

    const headers = {
        'Content-Type': 'application/json',
        ...(options.headers || {})
    };
    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    const config = { ...options, headers };
    try {
        const response = await fetch(url, config);
        if (!response.ok) {
            const error = await response.json().catch(() => ({}));
            throw new Error(error.message || `Ошибка ${response.status}`);
        }
        return await response.json();
    } catch (error) {
        console.error('API error:', error);
        throw error;
    }
}