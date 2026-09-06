import { requireServerSession } from '../../core/sessionBootstrap.js';

try {
    const session = await requireServerSession('patient');
    if (session) {
        // Import the dashboard modules only after the HttpOnly-cookie session has
        // restored non-secret display metadata into this tab's sessionStorage.
        await import('./patientDashboard.js');
        await import('../public/myReviews.js');
    }
} catch (err) {
    console.error('Patient session bootstrap failed:', err?.message || err);
    const message = document.createElement('div');
    message.className = 'panel-error';
    message.style.margin = '24px';
    message.textContent = 'Не удалось проверить сеанс. Обновите страницу или проверьте соединение.';
    document.body.prepend(message);
}
