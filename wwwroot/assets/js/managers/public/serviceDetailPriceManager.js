import { apiFetch } from '../../services/apiClient.js';
import { getLang } from '../../core/i18n.js';
import {
    buildDetailPriceSlots,
    formatDetailServicePriceParts,
} from './serviceDetailPricing.js';

function isServiceDetailPage(pathname) {
    const path = String(pathname || '');
    return path.startsWith('/pages/services/') && path.endsWith('.html');
}

export function installServiceDetailPriceManager() {
    if (typeof document === 'undefined' || typeof window === 'undefined') return;
    if (!isServiceDetailPage(window.location.pathname)) return;

    let services = [];
    let priceCards = [];

    const render = (lang = getLang()) => {
        if (!priceCards.length || !services.length) return;

        const slots = buildDetailPriceSlots(services, window.location.pathname, priceCards.length);
        for (const { slotIndex, service } of slots) {
            const card = priceCards[slotIndex];
            const price = card?.querySelector('.card__price');
            if (!price) continue;

            const parts = formatDetailServicePriceParts(service, lang);
            if (!parts) continue;

            let amount = price.querySelector('.amount');
            let currency = price.querySelector('.currency');

            if (!amount) {
                amount = document.createElement('span');
                amount.className = 'amount';
                price.prepend(amount);
            }
            if (!currency) {
                currency = document.createElement('span');
                currency.className = 'currency';
                price.appendChild(currency);
            }

            amount.textContent = parts.amount;
            currency.textContent = parts.currency;
            price.dataset.livePrice = 'true';
            price.dataset.serviceId = String(service.id ?? '');
        }
    };

    const load = async () => {
        priceCards = [...document.querySelectorAll('.service-detail-page .card--pricing')];
        if (!priceCards.length) return;

        try {
            const response = await apiFetch('/service');
            services = Array.isArray(response) ? response : [];
            render();
        } catch (error) {
            // Progressive enhancement: static prices remain visible when the API is unavailable.
            console.warn('Live service detail prices are unavailable:', error?.message || error);
        }
    };

    document.addEventListener('i18n:changed', event => render(event.detail?.lang || getLang()));

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', load, { once: true });
    } else {
        void load();
    }
}
