// Внутренний хелпер: разбирает "YYYY-MM-DDTHH:mm[:ss]" на части
function _parse(value) {
    if (!value) return null;
    const [datePart, timePartRaw] = value.split('T');
    const [y, m, d] = (datePart || '').split('-');
    const [hh, mm] = (timePartRaw || '').split(':');
    if (!y || !m || !d) return null;
    return { y, m, d, hh, mm };
}

/** "YYYY-MM-DD[THH:mm]" → "ДД.ММ.ГГГГ" | null/пусто → "—" */
export function formatDate(value) {
    const p = _parse(value);
    return p ? `${p.d}.${p.m}.${p.y}` : '—';
}

/** "YYYY-MM-DDTHH:mm[:ss]" → "ЧЧ:ММ" | нет времени или 00:00 → "—" */
export function formatTime(value) {
    const p = _parse(value);
    if (!p?.hh || !p?.mm || (p.hh === '00' && p.mm === '00')) return '—';
    return `${p.hh}:${p.mm}`;
}

/** "YYYY-MM-DDTHH:mm[:ss]" → "ДД.ММ.ГГГГ ЧЧ:ММ" | null/пусто → "Не назначено" */
export function formatDateTime(value) {
    const p = _parse(value);
    if (!p?.hh || !p?.mm) return value || 'Не назначено';
    return `${p.d}.${p.m}.${p.y} ${p.hh}:${p.mm}`;
}

/** "YYYY-MM-DDTHH:mm[:ss]" → "YYYY-MM-DDTHH:mm" для input[type="datetime-local"] */
export function toInputDateTime(value) {
    return value ? value.slice(0, 16) : '';
}

/** Date объект → "YYYY-MM-DD" (для DoctorCalendarManager) */
export function dateToString(d) {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}