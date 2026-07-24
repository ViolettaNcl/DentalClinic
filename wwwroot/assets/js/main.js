import { AuthManager } from '/assets/js/managers/auth/authManager.js';
import { NavigationManager } from '/assets/js/core/navigationManager.js';
import { LanguageSwitcher } from '/assets/js/core/languageSwitcher.js';
import { ChatBot } from '/assets/js/core/chatBot.js';
import { NotificationBell } from '/assets/js/core/notificationBell.js';
import { ready as i18nReady } from '/assets/js/core/i18n.js';

async function initializePage() {
    try {
        const response = await fetch('/pages/header.html');
        const html = await response.text();
        const doc = new DOMParser().parseFromString(html, 'text/html');

        const headerHTML = doc.querySelector('header').outerHTML;
        // Колокольчик может отсутствовать в разметке (например, если файл
        // header.html ещё не обновлён) — не даём этому сломать вставку шапки.
        const bellHTML = doc.querySelector('#notification-bell')?.outerHTML || '';
        const modalsHTML = doc.querySelector('#login-modal').outerHTML +
            doc.querySelector('#signup-modal').outerHTML;
        const footerHTML = doc.querySelector('footer').outerHTML;

        // Колокольчик уведомлений вставляем отдельно от шапки — он плавающий
        // элемент с fixed-позиционированием и не должен занимать место в хедере.
        document.body.insertAdjacentHTML('afterbegin', headerHTML + bellHTML + modalsHTML);
        document.body.insertAdjacentHTML('beforeend', footerHTML);

        // Дожидаемся загрузки словаря текущего языка ДО того, как остальные
        // модули начнут генерировать динамический текст (тосты, списки,
        // подписи кнопок) — иначе первая отрисовка всегда будет на русском.
        await i18nReady;

        const auth = new AuthManager();
        auth.init();

        const bell = new NotificationBell();
        bell.init();

        const nav = new NavigationManager();
        nav.init();

        const langSwitcher = new LanguageSwitcher();
        await langSwitcher.init();

        const bot = new ChatBot();
        bot.init();
    } catch (err) {
        console.error('Ошибка загрузки header:', err);
    }
}

document.addEventListener('DOMContentLoaded', initializePage);