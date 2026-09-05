export async function terminateAdminSession({ requestLogout, clearSession, redirect }) {
    if (typeof requestLogout !== 'function' || typeof clearSession !== 'function' || typeof redirect !== 'function') {
        throw new TypeError('Admin session termination requires logout, cleanup and redirect handlers');
    }

    // The JWT is stored in an HttpOnly cookie, so local UI cleanup alone is not
    // a logout. Expire the server-issued cookie first; only then clear display
    // metadata and leave the protected admin area.
    await requestLogout();
    clearSession();
    redirect('/index.html');
}
