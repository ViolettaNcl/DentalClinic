import { apiFetch } from '../services/apiClient.js';

const SESSION_KEYS = ['patientId', 'patientName', 'patientEmail', 'userRole', 'authToken'];
let currentSessionPromise = null;

function getDefaultStorage() {
    return typeof sessionStorage === 'undefined' ? null : sessionStorage;
}

export function clearSessionMetadata(storage = getDefaultStorage()) {
    if (!storage) return;
    SESSION_KEYS.forEach(key => storage.removeItem(key));
}

export function syncSessionMetadata(session, storage = getDefaultStorage()) {
    if (!storage || !session) return session;

    storage.setItem('patientId', String(session.id));
    storage.setItem('patientName', session.name || '');
    storage.setItem('patientEmail', session.email || '');
    storage.setItem('userRole', String(session.role || '').toLowerCase());
    storage.removeItem('authToken');
    return session;
}

export async function getServerSession({
    force = false,
    request = apiFetch,
    storage = getDefaultStorage()
} = {}) {
    if (!force && currentSessionPromise) return currentSessionPromise;

    const task = (async () => {
        try {
            const session = await request('/auth/session');
            return syncSessionMetadata(session, storage);
        } catch (error) {
            if (error?.status === 401 || error?.status === 403) {
                clearSessionMetadata(storage);
                return null;
            }
            throw error;
        }
    })();

    currentSessionPromise = task;
    return task;
}

export async function requireServerSession(expectedRole, {
    request = apiFetch,
    storage = getDefaultStorage(),
    redirect = url => window.location.replace(url),
    redirectUrl = '/index.html'
} = {}) {
    const session = await getServerSession({ force: true, request, storage });
    const actualRole = String(session?.role || '').toLowerCase();
    const requiredRole = String(expectedRole || '').toLowerCase();

    if (!session || (requiredRole && actualRole !== requiredRole)) {
        clearSessionMetadata(storage);
        redirect(redirectUrl);
        return null;
    }

    return session;
}

export function resetSessionBootstrapCache() {
    currentSessionPromise = null;
}
