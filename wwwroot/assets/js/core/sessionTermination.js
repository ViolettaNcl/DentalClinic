export async function terminateCookieSession({ requestLogout, clearSession, redirect, redirectUrl = '/index.html' }) {
    if (typeof requestLogout !== 'function' || typeof clearSession !== 'function' || typeof redirect !== 'function') {
        throw new TypeError('Session termination requires logout, cleanup and redirect handlers');
    }

    // The authenticated session lives in an HttpOnly cookie. Local UI metadata is
    // not enough to log out: if the server request fails, keep the browser state
    // intact and report the failure instead of pretending the session ended.
    await requestLogout();
    clearSession();
    redirect(redirectUrl);
}
