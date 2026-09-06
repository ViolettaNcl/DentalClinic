import { terminateCookieSession } from './sessionTermination.js';

export async function terminateAdminSession({ requestLogout, clearSession, redirect }) {
    return terminateCookieSession({ requestLogout, clearSession, redirect });
}
