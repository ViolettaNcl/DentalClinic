/**
 * Runs page initializers both during normal parser-driven loading and when a
 * module is imported after DOMContentLoaded (for example after an async session
 * check). A plain DOMContentLoaded listener silently misses that second case.
 */
export function runWhenDomReady(callback, documentRef = globalThis.document) {
    if (!documentRef || typeof callback !== 'function') return;

    if (documentRef.readyState === 'loading') {
        documentRef.addEventListener('DOMContentLoaded', callback, { once: true });
        return;
    }

    // Defer one microtask so a dynamically imported module can finish exporting
    // and its caller can apply any required guards before initialization begins.
    const schedule = globalThis.queueMicrotask
        || (task => Promise.resolve().then(task));
    schedule(callback);
}
