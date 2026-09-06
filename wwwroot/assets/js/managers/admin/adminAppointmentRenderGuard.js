import { escapeHtml } from '../../services/ui.js';

/**
 * Hardens the legacy appointment-table renderer in adminDashboard.js.
 *
 * Appointment rows already escape patient-controlled fields, but doctor names come
 * from the database through window.DoctorsDictionary and are interpolated directly
 * into innerHTML by AdminRequestsManager._doctor(). A crafted stored doctor name can
 * therefore become markup in the admin panel.
 *
 * adminDashboard's DOMContentLoaded listener awaits loadDoctors() before publishing
 * AdminRequestsManagerInstance. A later DOMContentLoaded listener is therefore not a
 * reliable ordering boundary: it can run while the first listener is suspended. We
 * install a one-shot property setter immediately so the manager is hardened at the
 * exact moment it is published, before the pending loadAll() promise can resume.
 */
export function installAdminAppointmentRenderGuard() {
    if (typeof window === 'undefined') return;

    const harden = manager => {
        if (!manager || manager.__storedDoctorNameGuard) return;

        manager.__storedDoctorNameGuard = true;
        manager._doctor = function safeDoctorName(id) {
            if (!id) return '—';
            const fullName = window.DoctorsDictionary?.[id]?.fullName;
            return fullName == null ? '—' : (escapeHtml(fullName) || '—');
        };
    };

    const property = 'AdminRequestsManagerInstance';
    const current = window[property];
    if (current) {
        harden(current);
        return;
    }

    const descriptor = Object.getOwnPropertyDescriptor(window, property);
    if (descriptor && !descriptor.configurable) {
        // Defensive fallback for an unexpected host-defined property. The production
        // dashboard does not define this property before assignment, but do not throw
        // and break the admin bootstrap if a third-party script changes that contract.
        queueMicrotask(() => harden(window[property]));
        return;
    }

    let pendingValue;
    Object.defineProperty(window, property, {
        configurable: true,
        enumerable: true,
        get() {
            return pendingValue;
        },
        set(value) {
            pendingValue = value;
            harden(value);

            // Restore a normal writable data property after the one assignment. This
            // keeps the global's legacy semantics for any later dashboard code.
            Object.defineProperty(window, property, {
                value,
                writable: true,
                configurable: true,
                enumerable: true
            });
        }
    });
}
