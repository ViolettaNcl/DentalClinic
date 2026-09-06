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
