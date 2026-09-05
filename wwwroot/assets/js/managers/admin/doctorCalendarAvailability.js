import { apiFetch } from '../../services/apiClient.js';
import { showError } from '../../services/ui.js';

function parseLocalDate(value) {
    const [year, month, day] = String(value).split('-').map(Number);
    return new Date(year, month - 1, day);
}

function dateKey(date) {
    return [
        date.getFullYear(),
        String(date.getMonth() + 1).padStart(2, '0'),
        String(date.getDate()).padStart(2, '0')
    ].join('-');
}

function renderAvailability(calendar, weekStart, availability) {
    const headers = document.querySelectorAll('.panel-table-calendar thead tr th');
    const monday = parseLocalDate(weekStart);
    const dayMap = Object.fromEntries((availability.days || []).map(day => [day.date, day]));

    for (let i = 0; i < 6; i++) {
        const cell = headers[i + 1];
        if (!cell) continue;
        const date = new Date(monday);
        date.setDate(date.getDate() + i);
        const base = cell.dataset.label || cell.textContent.trim().split(' ')[0];
        cell.dataset.label = base;
        cell.textContent = `${base} ${String(date.getDate()).padStart(2, '0')}.${String(date.getMonth() + 1).padStart(2, '0')}`;
    }

    const weekdays = [];
    for (let i = 0; i < 6; i++) {
        const date = new Date(monday);
        date.setDate(date.getDate() + i);
        weekdays.push(dayMap[dateKey(date)] || null);
    }

    const times = [...new Set(
        weekdays.flatMap(day => (day?.slots || []).map(slot => slot.time))
    )].sort();

    if (!times.length) {
        calendar.tbody.innerHTML = '<tr><td colspan="7">На этой неделе нет доступных рабочих слотов</td></tr>';
        return;
    }

    calendar.tbody.innerHTML = times.map(time => {
        let row = `<tr><td>${time}</td>`;

        weekdays.forEach(day => {
            if (!day || day.closed) {
                row += '<td><span class="slot slot-busy">Закрыто</span></td>';
                return;
            }

            const slot = (day.slots || []).find(item => item.time === time);
            if (!slot) {
                row += '<td><span class="slot">—</span></td>';
                return;
            }

            if (slot.isAvailable) {
                row += '<td><span class="slot slot-free">Свободно</span></td>';
                return;
            }

            const label = slot.blockedReason === 'pending'
                ? 'Заявка'
                : slot.blockedReason === 'confirmed'
                    ? 'Запись'
                    : 'Недоступно';
            const title = slot.appointmentId
                ? `Занято заявкой #${slot.appointmentId}`
                : 'Слот нельзя забронировать сейчас';
            row += `<td><span class="slot slot-busy" title="${title}">${label}</span></td>`;
        });

        return row + '</tr>';
    }).join('');
}

/**
 * Upgrades the calendar instance created by adminDashboard.js without adding a
 * second set of change listeners. Existing listeners call calendar.refresh(),
 * so replacing that method is enough to keep the panel behavior intact.
 */
export function installDoctorCalendarAvailability() {
    let attempts = 0;

    const install = () => {
        const calendar = window.DoctorCalendarManagerInstance;
        if (!calendar) {
            if (attempts++ < 80) window.setTimeout(install, 50);
            return;
        }
        if (calendar.__availabilityV2) return;

        calendar.__availabilityV2 = true;
        calendar.refresh = async function refreshAvailability() {
            const doctorId = parseInt(this.select?.value, 10);
            if (!doctorId) {
                if (this.tbody) this.tbody.innerHTML = '<tr><td colspan="7">Выберите врача</td></tr>';
                return;
            }

            if (this.tbody) this.tbody.innerHTML = '<tr><td colspan="7">Загрузка...</td></tr>';
            const { from, to } = this._weekRange();

            try {
                const data = await apiFetch(`/doctorschedule/availability?doctorId=${doctorId}&from=${from}&to=${to}`);
                renderAvailability(this, from, data);
            } catch (err) {
                console.error('Doctor calendar availability error:', err);
                if (this.tbody) this.tbody.innerHTML = '<tr><td colspan="7">Ошибка загрузки</td></tr>';
                showError('Ошибка загрузки доступности врача');
            }
        };

        calendar.refresh();
    };

    install();
}
