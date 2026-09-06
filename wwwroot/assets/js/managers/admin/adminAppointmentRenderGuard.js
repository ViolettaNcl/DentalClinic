import { escapeHtml } from '../../services/ui.js';

/**
 * Hardens the legacy appointment-table renderer in adminDashboard.js.
 *
 * Appointment rows already escape patient-controlled fields, but doctor names come
 * from the database through window.DoctorsDictionary and are interpolated directly
 * into innerHTML by AdminRequestsManager._doctor(). A crafted stored doctor name can
 * therefore become markup in the admin panel. Patch the existing manager instance
 * after adminDashboard.js creates it, without adding a second set of table handlers.
 */
export function installAdminAppointmentRenderGuard() {
    if (typeof window === 'undefined' || typeof document === 'undefined') return;

    const install = () => {
        const manager = window.AdminRequestsManagerInstance;
        if (!manager || manager.__storedDoctorNameGuard) return;

        manager.__storedDoctorNameGuard = true;
        manager._doctor = function safeDoctorName(id) {
            if (!id) return '—';
            const fullName = window.DoctorsDictionary?.[id]?.fullName;
            return fullName == null ? '—' : (escapeHtml(fullName) || '—');
        };
    };

    if (document.readyState === 'loading') {
        // adminDashboard.js is loaded before doctorsManager.js, so its DOMContentLoaded
        // listener creates AdminRequestsManagerInstance first. This listener then patches
        // the renderer before the async appointment request can normally complete.
        document.addEventListener('DOMContentLoaded', install, { once: true });
    } else {
        install();
    }
}
