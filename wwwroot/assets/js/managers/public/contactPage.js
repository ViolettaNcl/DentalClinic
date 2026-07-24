/**
 * Креативная кнопка "!" у карты: подсказка при наведении (чистый CSS)
 * + построение маршрута до клиники по геолокации пользователя при клике.
 */
import { t, onLanguageChange } from '../../core/i18n.js';

const CLINIC = { lat: 48.709737, lng: 44.516499 };

// Расстояние по прямой между двумя точками (формула гаверсинуса), км
function distanceKm(lat1, lng1, lat2, lng2) {
    const R = 6371;
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) ** 2 +
        Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
        Math.sin(dLng / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function initRouteBuilder() {
    const btn = document.getElementById('route-fab');
    const resultEl = document.getElementById('route-result');
    if (!btn || !resultEl) return;

    const label = btn.querySelector('.route-fab-label');
    if (label) {
        label.textContent = t('route_btn_label', 'Построить маршрут');
        onLanguageChange(() => { label.textContent = t('route_btn_label', 'Построить маршрут'); });
    }

    const manualLinkText = () => t('route_open_manual', 'Открыть маршрут вручную →');

    btn.addEventListener('click', () => {
        if (!navigator.geolocation) {
            resultEl.innerHTML = `${t('route_no_geolocation', 'Геолокация не поддерживается вашим браузером.')} <a href="https://www.google.com/maps/dir/?api=1&destination=${CLINIC.lat},${CLINIC.lng}" target="_blank" rel="noopener">${manualLinkText()}</a>`;
            resultEl.classList.add('is-visible');
            return;
        }

        btn.classList.add('is-loading');
        btn.disabled = true;

        navigator.geolocation.getCurrentPosition(
            (pos) => {
                const { latitude, longitude } = pos.coords;
                const km = distanceKm(latitude, longitude, CLINIC.lat, CLINIC.lng);
                const minutes = Math.max(3, Math.round((km / 32) * 60)); // ~32 км/ч по городу, ориентировочно

                const mapsUrl = `https://www.google.com/maps/dir/?api=1&origin=${latitude},${longitude}&destination=${CLINIC.lat},${CLINIC.lng}&travelmode=driving`;

                const distanceText = t('route_distance_text', 'Вы примерно в {km} км от клиники — около {min} мин на машине.')
                    .replace('{km}', `<strong>${km.toFixed(1)}</strong>`)
                    .replace('{min}', `<strong>${minutes}</strong>`);

                resultEl.innerHTML = `
                    ${distanceText}
                    <br><a href="${mapsUrl}" target="_blank" rel="noopener">${t('route_open_in_maps', 'Открыть маршрут в Google Картах →')}</a>
                `;
                resultEl.classList.add('is-visible');

                btn.classList.remove('is-loading');
                btn.disabled = false;
            },
            (err) => {
                console.warn('Геолокация недоступна:', err.message);
                resultEl.innerHTML = `${t('route_geolocation_denied', 'Не удалось определить ваше местоположение — разрешите доступ к геолокации в браузере.')} <br><a href="https://www.google.com/maps/dir/?api=1&destination=${CLINIC.lat},${CLINIC.lng}" target="_blank" rel="noopener">${manualLinkText()}</a>`;
                resultEl.classList.add('is-visible');

                btn.classList.remove('is-loading');
                btn.disabled = false;
            },
            { timeout: 10000, maximumAge: 300000 }
        );
    });

    // Клик вне подсказки/тоста скрывает результат обратно
    document.addEventListener('click', (e) => {
        if (!resultEl.contains(e.target) && e.target !== btn) {
            resultEl.classList.remove('is-visible');
        }
    });
}

document.addEventListener('DOMContentLoaded', initRouteBuilder);