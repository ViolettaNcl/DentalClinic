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

// A slow remote database answers 503 (not 401). Retry a couple of times instead of
// treating the visitor as logged out right after a successful sign-in.
async function requestServerSessionWithRetry() {
    const delays = [600, 1500];
    for (let attempt = 0; ; attempt += 1) {
        try {
            return await requestServerSession();
        } catch (error) {
            const retryable = error?.status === 503 || error?.status === 502 || error?.status === 504
                || error instanceof TypeError;
            if (!retryable || attempt >= delays.length) throw error;
            await new Promise(resolve => setTimeout(resolve, delays[attempt]));
        }
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
    request = requestServerSessionWithRetry,
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
    request = requestServerSessionWithRetry,
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
