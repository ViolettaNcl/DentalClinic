const SESSION_KEYS = ['patientId', 'patientName', 'patientEmail', 'userRole', 'authToken'];
const SESSION_REQUEST_TIMEOUT_MS = 25000;
let currentSessionPromise = null;

function getDefaultStorage() {
    return typeof sessionStorage === 'undefined' ? null : sessionStorage;
}

async function requestServerSession() {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), SESSION_REQUEST_TIMEOUT_MS);

    try {
        const response = await fetch('/api/auth/session', {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' },
            signal: controller.signal
        });

        if (response.status === 401 || response.status === 403) return null;
        if (!response.ok) {
            const payload = await response.json().catch(() => ({}));
            const error = new Error(payload.message || `Ошибка ${response.status}`);
            error.status = response.status;
            throw error;
        }

        return response.json();
    } catch (error) {
        if (error?.name === 'AbortError') {
            const timeout = new Error('Сервер слишком долго проверяет сеанс. Проверьте подключение к базе данных и повторите попытку.');
            timeout.code = 'SESSION_TIMEOUT';
            throw timeout;
        }
        throw error;
    } finally {
        clearTimeout(timer);
    }
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
    request = requestServerSession,
    storage = getDefaultStorage()
} = {}) {
    if (!force && currentSessionPromise) return currentSessionPromise;

    const task = (async () => {
        const session = await request();
        if (!session) {
            clearSessionMetadata(storage);
            return null;
        }
        return syncSessionMetadata(session, storage);
    })();

    currentSessionPromise = task;
    return task;
}

export async function requireServerSession(expectedRole, {
    request = requestServerSession,
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
