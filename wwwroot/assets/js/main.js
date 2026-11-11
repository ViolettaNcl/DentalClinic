import { AuthManager } from '/assets/js/managers/auth/authManager.js';
import { NavigationManager } from '/assets/js/core/navigationManager.js';
import { LanguageSwitcher } from '/assets/js/core/languageSwitcher.js';
import { ChatBot } from '/assets/js/core/chatBot.js';
import { isBookingIntent } from '/assets/js/core/bookingIntent.js';
import { installDentaSafetyGuard } from '/assets/js/core/dentaSafetyGuard.js';
import { installChatBookingCookieTransport } from '/assets/js/core/chatBookingTransport.js';
import { installDentaReplyAvatar } from '/assets/js/core/dentaReplyAvatar.js';
import { NotificationBell } from '/assets/js/core/notificationBell.js';
import { ready as i18nReady } from '/assets/js/core/i18n.js';
import { installServiceDetailPriceManager } from '/assets/js/managers/public/serviceDetailPriceManager.js';
import { installPublicDoctorCatalogSync } from '/assets/js/managers/public/publicDoctorCatalogManager.js';
import { PublicReviewsManager } from '/assets/js/managers/public/reviewsManager.js';
import { escapeHtmlAttribute } from '/assets/js/services/htmlAttributeSafety.js';

ChatBot.prototype._isBookingIntent = isBookingIntent;
installDentaSafetyGuard(ChatBot);
installChatBookingCookieTransport(ChatBot);
installServiceDetailPriceManager();
installPublicDoctorCatalogSync();

// Public review text is reused inside a quoted data-* attribute so that the
// translation button can restore the original. Text-node escaping alone does not
// escape quotes and therefore is insufficient for that attribute context. Patch
// the renderer before DOMContentLoaded so an approved review cannot break out of
// data-original-text and inject event-handler attributes into the public page.
PublicReviewsManager.prototype._escape = escapeHtmlAttribute;

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

        // Header controls must work the moment they are visible. They used to be
        // wired only after i18n + the auth session check, so an early click on the
        // language button or burger menu was silently lost (also broke Production E2E).
        const nav = new NavigationManager(); nav.init();
        const langSwitcher = new LanguageSwitcher();
        const langReady = langSwitcher.init();

        await i18nReady;

        const auth = new AuthManager(); await auth.init();
        const bell = new NotificationBell(); bell.init();
        await langReady;

        // Install the reply-avatar observer before Denta renders any messages.
        // It upgrades both streamed and fallback bot bubbles to the same real
        // generated operator portrait and leaves the CSS background as fallback.
        installDentaReplyAvatar();
        const bot = new ChatBot(); bot.init();
    } catch (err) {
        console.error('Ошибка загрузки header:', err);
    }
}

document.addEventListener('DOMContentLoaded', initializePage);
