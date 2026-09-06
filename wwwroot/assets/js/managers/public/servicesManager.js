import { apiFetch } from '../../services/apiClient.js';
import { getLang, onLanguageChange } from '../../core/i18n.js';
import { buildStartingPricesByPage, formatStartingPrice } from './servicePriceSummary.js';

const serviceLinks = {
    cosmetic: '/pages/services/cosmetic-treatments.html',
    fillings: '/pages/services/fillings.html',
    crowns: '/pages/services/crowns.html',
    implants: '/pages/services/implants.html',
    'root-canal': '/pages/services/root-canal.html',
    bridges: '/pages/services/bridges.html',
    extractions: '/pages/services/extractions.html',
    dentures: '/pages/services/prosthetics.html',
};

function renderLivePrices(cards, startingPrices, lang) {
    cards.forEach(card => {
        const pageUrl = serviceLinks[card.id];
        if (!pageUrl) return;

        const body = card.querySelector('.card__body');
        if (!body) return;

        let badge = body.querySelector('.card__live-price');
        const amount = startingPrices[pageUrl];

        if (amount === undefined) {
            badge?.remove();
            return;
        }

        if (!badge) {
            badge = document.createElement('p');
            badge.className = 'card__live-price';
            badge.setAttribute('aria-live', 'polite');
            body.appendChild(badge);
        }

        badge.textContent = formatStartingPrice(amount, lang);
    });
}

document.addEventListener('DOMContentLoaded', async () => {
    const cards = Array.from(document.querySelectorAll('.card'));
    let startingPrices = {};

    cards.forEach(card => {
        const url = serviceLinks[card.id];
        if (!url) return;

        card.style.cursor = 'pointer';
        card.setAttribute('role', 'link');
        card.setAttribute('tabindex', '0');

        const navigate = () => { window.location.href = url; };
        card.addEventListener('click', navigate);
        card.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                navigate();
            }
        });
    });

    onLanguageChange(lang => renderLivePrices(cards, startingPrices, lang));

    try {
        const services = await apiFetch('/service');
        startingPrices = buildStartingPricesByPage(services);
        renderLivePrices(cards, startingPrices, getLang());
    } catch (error) {
        // Prices are progressive enhancement: the static service catalogue stays
        // fully usable if the API is temporarily unavailable.
        console.warn('Live service prices are unavailable:', error?.message || error);
    }
});
