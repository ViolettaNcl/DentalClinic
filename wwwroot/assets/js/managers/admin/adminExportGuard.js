import { showError, showSuccess } from '../../services/ui.js';
import { clearSessionMetadata } from '../../core/sessionBootstrap.js';

export async function requestAdminExport(path, { fetchImpl = fetch } = {}) {
    const response = await fetchImpl(path, {
        credentials: 'same-origin',
        headers: { Accept: '*/*' }
    });

    if (response.status === 401 || response.status === 403) {
        const error = new Error('Сеанс администратора завершён');
        error.status = response.status;
        throw error;
    }

    if (!response.ok) {
        const error = new Error('Не удалось сформировать отчёт');
        error.status = response.status;
        throw error;
    }

    return response;
}

function handleSessionFailure(error) {
    if (error?.status !== 401 && error?.status !== 403) return false;
    clearSessionMetadata();
    window.location.replace('/index.html');
    return true;
}

function downloadBlob(blob, filename) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
}

let installed = false;

export function installAdminExportCookieGuard() {
    if (installed || typeof document === 'undefined') return;
    installed = true;

    document.addEventListener('click', async event => {
        const xlsxButton = event.target?.closest?.('#btn-export-xlsx');
        const pdfButton = event.target?.closest?.('#btn-export-pdf');
        if (!xlsxButton && !pdfButton) return;

        // adminDashboard.js still contains the legacy Authorization header path.
        // Capture first and replace it with a same-origin cookie-authenticated request.
        event.preventDefault();
        event.stopImmediatePropagation();

        if (xlsxButton) {
            xlsxButton.disabled = true;
            try {
                const response = await requestAdminExport('/api/adminstats/export/xlsx');
                const blob = await response.blob();
                downloadBlob(blob, `zayavki_${new Date().toISOString().slice(0, 10)}.xlsx`);
                showSuccess('Excel-файл сформирован');
            } catch (error) {
                if (!handleSessionFailure(error))
                    showError(error.message || 'Не удалось скачать Excel-файл');
            } finally {
                xlsxButton.disabled = false;
            }
            return;
        }

        const reportWindow = window.open('', '_blank');
        if (!reportWindow) {
            showError('Браузер заблокировал всплывающее окно — разрешите всплывающие окна для этого сайта');
            return;
        }

        reportWindow.document.write('<p style="font-family:sans-serif;padding:20px;">Формирование отчёта…</p>');
        pdfButton.disabled = true;
        try {
            const response = await requestAdminExport('/api/adminstats/export/report');
            const html = await response.text();
            reportWindow.document.open();
            reportWindow.document.write(html);
            reportWindow.document.close();
        } catch (error) {
            reportWindow.close();
            if (!handleSessionFailure(error))
                showError(error.message || 'Не удалось открыть отчёт');
        } finally {
            pdfButton.disabled = false;
        }
    }, true);
}
