const API_BASE = '/api';
const DEFAULT_TIMEOUT_MS = 38000;
const DEFAULT_GET_RETRIES = 1;
const GET_CONCURRENCY_LIMIT = 2;
const inFlightGets = new Map();

let activeGets = 0;
const pendingGetSlots = [];

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

function createTimedSignal(externalSignal, timeoutMs) {
    const controller = new AbortController();
    let timedOut = false;

    const onExternalAbort = () => controller.abort(externalSignal?.reason);
    if (externalSignal) {
        if (externalSignal.aborted) controller.abort(externalSignal.reason);
        else externalSignal.addEventListener('abort', onExternalAbort, { once: true });
    }

    const timer = Number.isFinite(timeoutMs) && timeoutMs > 0
        ? setTimeout(() => {
            timedOut = true;
            controller.abort(new DOMException('Request timed out', 'TimeoutError'));
        }, timeoutMs)
        : null;

    return {
        signal: controller.signal,
        didTimeout: () => timedOut,
        dispose() {
            if (timer) clearTimeout(timer);
            externalSignal?.removeEventListener?.('abort', onExternalAbort);
        }
    };
}

async function acquireGetSlot() {
    if (activeGets < GET_CONCURRENCY_LIMIT) {
        activeGets += 1;
        return;
    }

    await new Promise(resolve => pendingGetSlots.push(resolve));
    activeGets += 1;
}

function releaseGetSlot() {
    activeGets = Math.max(0, activeGets - 1);
    const next = pendingGetSlots.shift();
    if (next) next();
}

function delay(ms, signal) {
    if (!ms) return Promise.resolve();
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
            signal?.removeEventListener?.('abort', onAbort);
            resolve();
        }, ms);
        const onAbort = () => {
            clearTimeout(timer);
            reject(signal?.reason || new DOMException('Aborted', 'AbortError'));
        };
        if (signal?.aborted) onAbort();
        else signal?.addEventListener?.('abort', onAbort, { once: true });
    });
}

function shouldRetryGet(error, attempt, maxRetries, externalSignal) {
    if (attempt >= maxRetries || externalSignal?.aborted) return false;
    if (error?.name === 'AbortError' || error?.name === 'TimeoutError') return false;

    if (error instanceof ApiError) {
        if (error.payload?.timeout) return true;
        return error.status === 503 || error.status === 504 || error.status === 502;
    }

    // A TypeError from fetch normally means a transport/network failure. Retrying a
    // GET once is safe and is useful with the remote development SQL host.
    return error instanceof TypeError;
}

async function performRequestOnce(url, endpoint, options) {
    const {
        timeoutMs = DEFAULT_TIMEOUT_MS,
        signal: externalSignal,
        dedupe: _dedupe,
        retries: _retries,
        ...fetchOptions
    } = options;

    const isFormData = typeof FormData !== 'undefined' && fetchOptions.body instanceof FormData;
    const headers = {
        ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
        ...(fetchOptions.headers || {})
    };

    const timed = createTimedSignal(externalSignal, timeoutMs);
    const config = {
        ...fetchOptions,
        headers,
        credentials: fetchOptions.credentials || 'same-origin',
        signal: timed.signal
    };

    try {
        const response = await fetch(url, config);
        if (!response.ok) {
            const payload = await response.json().catch(() => ({}));
            if (response.status === 401)
                clearLocalSessionMetadata();

            throw new ApiError(
                payload.message || `Ошибка ${response.status}`,
                response.status,
                payload);
        }

        if (response.status === 204) return null;
        return await response.json();
    } catch (error) {
        if (timed.didTimeout()) {
            const timeoutError = new ApiError(
                'Сервер отвечает слишком долго. Попробуйте ещё раз.',
                null,
                { endpoint, timeout: true });
            console.warn('API request timed out:', endpoint);
            throw timeoutError;
        }

        if (error?.name === 'AbortError' || error?.name === 'TimeoutError') {
            // Navigation, tab changes and page unloads can cancel fetches. Preserve
            // the cancellation without converting it into a misleading server error.
            throw error;
        }

        throw error;
    } finally {
        timed.dispose();
    }
}

async function performRequest(url, endpoint, options) {
    const method = String(options.method || 'GET').toUpperCase();
    const externalSignal = options.signal;
    const maxRetries = method === 'GET'
        ? Math.max(0, Math.min(Number(options.retries ?? DEFAULT_GET_RETRIES), 2))
        : 0;

    if (method === 'GET') await acquireGetSlot();

    try {
        for (let attempt = 0; ; attempt += 1) {
            try {
                return await performRequestOnce(url, endpoint, options);
            } catch (error) {
                if (!shouldRetryGet(error, attempt, maxRetries, externalSignal)) {
                    if (error?.name !== 'AbortError' && error?.name !== 'TimeoutError')
                        console.error('API request failed:', error?.message || 'unknown error');
                    throw error;
                }

                const retryDelay = 350 * (attempt + 1);
                console.warn(`Temporary API failure for ${endpoint}; retrying once in ${retryDelay}ms`);
                await delay(retryDelay, externalSignal);
            }
        }
    } finally {
        if (method === 'GET') releaseGetSlot();
    }
}

export function apiFetch(endpoint, options = {}) {
    const url = `${API_BASE}${endpoint}`;
    const method = String(options.method || 'GET').toUpperCase();

    // Several dashboard modules can request the same GET during one paint. Share the
    // in-flight promise rather than multiplying identical remote SQL work.
    if (method === 'GET' && options.dedupe !== false) {
        const key = `${method}:${url}`;
        const existing = inFlightGets.get(key);
        if (existing) return existing;

        const task = performRequest(url, endpoint, options)
            .finally(() => {
                if (inFlightGets.get(key) === task) inFlightGets.delete(key);
            });
        inFlightGets.set(key, task);
        return task;
    }

    return performRequest(url, endpoint, options);
}
