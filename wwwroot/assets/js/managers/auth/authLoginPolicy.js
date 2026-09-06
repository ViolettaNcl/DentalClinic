// Pure login-routing policy. The shared login form first checks the patient
// endpoint, then falls back to the admin endpoint only when credentials were
// explicitly rejected (401). Operational failures must never trigger a second
// authentication request.
export function shouldTryAdminFallback(error) {
    return Number(error?.status) === 401;
}
