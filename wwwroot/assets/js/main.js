import { AuthManager } from '/assets/js/managers/auth/authManager.js';
import { NavigationManager } from '/assets/js/core/navigationManager.js';
import { LanguageSwitcher } from '/assets/js/core/languageSwitcher.js';
import { ChatBot } from '/assets/js/core/chatBot.js';
import { isBookingIntent } from '/assets/js/core/bookingIntent.js';
import { installDentaSafetyGuard } from '/assets/js/core/dentaSafetyGuard.js';
import { NotificationBell } from '/assets/js/core/notificationBell.js';
import { ready as i18nReady } from '/assets/js/core/i18n.js';
import { installServiceDetailPriceManager } from '/assets/js/managers/public/serviceDetailPriceManager.js';
import { installPublicDoctorCatalogSync } from '/assets/js/managers/public/publicDoctorCatalogManager.js';

ChatBot.prototype._isBookingIntent = isBookingIntent;
installDentaSafetyGuard(ChatBot);
installServiceDetailPriceManager();
installPublicDoctorCatalogSync();

async function initializePage() {
    try {
        const response = await fetch('/pages/header.html');
        const html = await response.text();
        const doc = new DOMParser().parseFromString(html, 'text/html');

        const headerHTML = doc.querySelector('header').outerHTML;
        const bellHTML = doc.querySelector('#notification-bell')?.outerHTML || '';
        const modalsHTML = doc.querySelector('#login-modal').outerHTML + doc.querySelector('#signup-modal').outerHTML;
        const footerHTML = doc.querySelector('footer').outerHTML;

        document.body.insertAdjacentHTML('afterbegin', headerHTML + bellHTML + modalsHTML);
        document.body.insertAdjacentHTML('beforeend', footerHTML);
        await i18nReady;

        const auth = new AuthManager(); auth.init();
        const bell = new NotificationBell(); bell.init();
        const nav = new NavigationManager(); nav.init();
        const langSwitcher = new LanguageSwitcher(); await langSwitcher.init();
        const bot = new ChatBot(); bot.init();
    } catch (err) {
        console.error('Ошибка загрузки header:', err);
    }
}

document.addEventListener('DOMContentLoaded', initializePage);
