/**
 * Public contact/location page behavior. Clinic facts come from server configuration
 * instead of placeholder addresses/coordinates embedded in JavaScript or markup.
 */
import { t, onLanguageChange } from '../../core/i18n.js';
import { getPublicClinicProfile } from '../../core/publicClinicProfile.js';

function distanceKm(lat1, lng1, lat2, lng2) {
    const R = 6371;
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) ** 2 +
        Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
        Math.sin(dLng / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function hydrateContactCards(profile) {
    document.querySelectorAll('.contact-cards-grid .contact-card').forEach(card => {
        const field = card.dataset.clinicField;
        const value = field && typeof profile[field] === 'string' ? profile[field] : null;
        const text = card.querySelector('.card__text');

        if (!text || !value) {
            card.hidden = true;
            return;
        }

        text.textContent = value;
        card.hidden = false;
    });
}

function prepareContactFactsForLoading() {
    document.querySelectorAll('.contact-cards-grid .contact-card').forEach(card => {
        card.hidden = true;
    });
    const location = document.querySelector('.location-section');
    if (location) location.hidden = true;
}

function configureMap(profile) {
    const location = document.querySelector('.location-section');
    const iframe = location?.querySelector('iframe');
    const btn = document.getElementById('route-fab');

    if (!profile.hasCoordinates) {
        if (location) location.hidden = true;
        if (iframe) {
            iframe.removeAttribute('src');
            iframe.hidden = true;
        }
        if (btn) btn.hidden = true;
        return false;
    }

    if (location) location.hidden = false;
    if (iframe) {
        const coordinates = encodeURIComponent(`${profile.latitude},${profile.longitude}`);
        iframe.src = `https://www.google.com/maps?q=${coordinates}&output=embed`;
        iframe.hidden = false;
        iframe.title = profile.address
            ? `Dental Clinic — ${profile.address}`
            : 'Dental Clinic location';
    }
    if (btn) btn.hidden = false;
    return true;
}

function renderRouteResult(resultEl, message, linkUrl = null, linkText = null) {
    resultEl.replaceChildren(document.createTextNode(message));

    if (linkUrl && linkText) {
        resultEl.appendChild(document.createElement('br'));
        const link = document.createElement('a');
        link.href = linkUrl;
        link.target = '_blank';
        link.rel = 'noopener';
        link.textContent = linkText;
        resultEl.appendChild(link);
    }

    resultEl.classList.add('is-visible');
}

function initRouteBuilder(profile) {
    const btn = document.getElementById('route-fab');
    const resultEl = document.getElementById('route-result');
    if (!btn || !resultEl || !profile.hasCoordinates) return;

    const clinic = { lat: profile.latitude, lng: profile.longitude };
    const label = btn.querySelector('.route-fab-label');
    if (label) {
        const refreshLabel = () => {
            label.textContent = t('route_btn_label', 'Построить маршрут');
        };
        refreshLabel();
        onLanguageChange(refreshLabel);
    }

    const manualLinkText = () => t('route_open_manual', 'Открыть маршрут вручную →');
    const clinicOnlyRoute = () =>
        `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(`${clinic.lat},${clinic.lng}`)}`;

    btn.addEventListener('click', () => {
        if (!navigator.geolocation) {
            renderRouteResult(
                resultEl,
                t('route_no_geolocation', 'Геолокация не поддерживается вашим браузером.'),
                clinicOnlyRoute(),
                manualLinkText());
            return;
        }

        btn.classList.add('is-loading');
        btn.disabled = true;

        navigator.geolocation.getCurrentPosition(
            pos => {
                const { latitude, longitude } = pos.coords;
                const km = distanceKm(latitude, longitude, clinic.lat, clinic.lng);
                const minutes = Math.max(3, Math.round((km / 32) * 60));
                const origin = encodeURIComponent(`${latitude},${longitude}`);
                const destination = encodeURIComponent(`${clinic.lat},${clinic.lng}`);
                const mapsUrl = `https://www.google.com/maps/dir/?api=1&origin=${origin}&destination=${destination}&travelmode=driving`;
                const distanceText = t(
                    'route_distance_text',
                    'Вы примерно в {km} км от клиники — около {min} мин на машине.')
                    .replace('{km}', km.toFixed(1))
                    .replace('{min}', String(minutes));

                renderRouteResult(
                    resultEl,
                    distanceText,
                    mapsUrl,
                    t('route_open_in_maps', 'Открыть маршрут в Google Картах →'));

                btn.classList.remove('is-loading');
                btn.disabled = false;
            },
            err => {
                console.warn('Геолокация недоступна:', err.message);
                renderRouteResult(
                    resultEl,
                    t('route_geolocation_denied', 'Не удалось определить ваше местоположение — разрешите доступ к геолокации в браузере.'),
                    clinicOnlyRoute(),
                    manualLinkText());

                btn.classList.remove('is-loading');
                btn.disabled = false;
            },
            { timeout: 10000, maximumAge: 300000 }
        );
    });

    document.addEventListener('click', event => {
        if (!resultEl.contains(event.target) && event.target !== btn) {
            resultEl.classList.remove('is-visible');
        }
    });
}

async function initContactPage() {
    prepareContactFactsForLoading();

    try {
        const profile = await getPublicClinicProfile();
        hydrateContactCards(profile);
        if (configureMap(profile)) initRouteBuilder(profile);
    } catch (err) {
        console.warn('Не удалось загрузить публичные данные клиники:', err?.message || err);
        configureMap({ hasCoordinates: false });
    }
}

document.addEventListener('DOMContentLoaded', initContactPage);
