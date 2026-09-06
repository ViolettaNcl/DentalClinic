// Обёртка над SignalR для живых уведомлений вместо опроса каждые 60 секунд.
// Библиотеку подключаем динамически из CDN — так не нужно вписывать <script> в head
// каждой страницы сайта (колокольчик есть в шапке почти везде).
const SIGNALR_CDN = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js';

let loadPromise = null;
function loadSignalRLib() {
    if (window.signalR) return Promise.resolve();
    if (loadPromise) return loadPromise;

    loadPromise = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = SIGNALR_CDN;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Не удалось загрузить SignalR'));
        document.head.appendChild(script);
    });
    return loadPromise;
}

class RealtimeService {
    constructor() {
        this.connection = null;
        this._handlers = {};
    }

    // SignalR подключается к тому же origin, поэтому защищённая dc_auth cookie
    // отправляется браузером автоматически. JWT намеренно не читается из JS и
    // не помещается в query string, где он мог бы попасть в URL/логи/историю.
    // Если сервер отвергнет анонимное соединение или сеть недоступна — остаёмся
    // без realtime, а обычная загрузка данных продолжит работать.
    async connect() {
        try {
            await loadSignalRLib();
        } catch (err) {
            console.warn('SignalR недоступен, работаем без realtime:', err);
            return;
        }

        if (this.connection) return;

        this.connection = new window.signalR.HubConnectionBuilder()
            .withUrl('/hubs/notifications')
            .withAutomaticReconnect()
            .build();

        for (const [event, handlers] of Object.entries(this._handlers)) {
            handlers.forEach(h => this.connection.on(event, h));
        }

        try {
            await this.connection.start();
        } catch (err) {
            console.warn('Не удалось подключиться к realtime-уведомлениям:', err);
        }
    }

    on(event, handler) {
        (this._handlers[event] ||= []).push(handler);
        this.connection?.on(event, handler);
    }

    async disconnect() {
        try { await this.connection?.stop(); } catch { /* ignore */ }
        this.connection = null;
    }
}

// Единый экземпляр на страницу
const realtime = new RealtimeService();
export { realtime };