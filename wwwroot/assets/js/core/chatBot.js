// =====================================================
// 🤖 AI ЧАТ-БОТ DENTAL CLINIC — PRO версия
// Функции: языки, ссылки, цены, симптомы,
//          проактивные сообщения, умная запись
// =====================================================

import { onLanguageChange } from './i18n.js';
import { translateText } from '../services/textTranslate.js';
import { showError } from '../services/ui.js';

const I18N_BASE_PATH = '/assets/i18n/';
const LANG_STORAGE_KEY = 'site_lang';
const RTL_LANGS = ['ar'];

// Проактивные триггеры: страница → задержка (мс) → сообщение боту
const PROACTIVE_TRIGGERS = [
    { path: '/pages/services/implants.html', delay: 25000, msg: '__proactive_implants__' },
    { path: '/pages/services/cosmetic-treatments.html', delay: 20000, msg: '__proactive_cosmetic__' },
    { path: '/pages/services/crowns.html', delay: 22000, msg: '__proactive_crowns__' },
    { path: '/pages/services/root-canal.html', delay: 20000, msg: '__proactive_canal__' },
    { path: '/pages/services/extractions.html', delay: 18000, msg: '__proactive_extraction__' },
    { path: '/pages/services/bridges.html', delay: 22000, msg: '__proactive_bridges__' },
    { path: '/pages/services/prosthetics.html', delay: 22000, msg: '__proactive_prosthetics__' },
    { path: '/pages/about.html', delay: 30000, msg: '__proactive_about__' },
    { path: '/pages/doctors.html', delay: 25000, msg: '__proactive_doctors__' },
    { path: '/', delay: 40000, msg: '__proactive_home__' },
    { path: '/index.html', delay: 40000, msg: '__proactive_home__' },
];

const FALLBACK_QUICK = [
    '🦷 Услуги и цены', '📅 Записаться', '👨‍⚕️ Врачи', '💉 Седация',
];

// Состояние диалога записи
const BOOKING_STEPS = ['idle', 'ask_name', 'ask_phone', 'ask_date', 'ask_comment', 'confirm', 'done'];

class ChatBot {
    constructor() {
        this.history = [];
        this.isOpen = false;
        this.isTyping = false;
        this.lang = this._detectLang();
        this.t = {};
        this.booking = { step: 'idle', name: '', phone: '', date: '', comment: '' };
        this._proTimer = null;
        this._proShown = sessionStorage.getItem('chat_proactive_shown') === '1';

        // Идентификатор диалога — объединяет сообщения этой вкладки в БД,
        // чтобы админ видел переписку целиком в разделе "Аналитика"
        this.sessionId = this._getOrCreateSessionId();

        // Голосовой ввод (Web Speech API)
        this._recognition = null;
        this._isListening = false;
        this._micStartPending = false;
        this._voiceUnavailableReason = null;
        this._voiceInputPrefix = '';
        this._micStoppedByUser = false;

        // Озвучка ответов бота (Web Speech Synthesis) — по умолчанию выключена
        this.ttsEnabled = localStorage.getItem('chat_tts_enabled') === '1';
    }

    _getOrCreateSessionId() {
        let id = sessionStorage.getItem('chat_session_id');
        if (!id) {
            id = (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`).replace(/-/g, '');
            sessionStorage.setItem('chat_session_id', id);
        }
        return id;
    }

    async init() {
        await this._loadTranslations();
        this._render();
        this._bindEvents();
        this._initProactive();
        this._initVoiceRecognition();

        // Живое переключение языка: срабатывает сразу при клике на переключатель
        // языка в шапке сайта, даже если чат уже открыт и в нём есть переписка —
        // не нужно ждать перезагрузки страницы или закрытия/открытия чата.
        onLanguageChange((newLang) => this._onSiteLanguageChanged(newLang));
    }

    // Вызывается при смене языка сайта (core/i18n.js -> setLang()).
    // Обновляет статичные тексты виджета "на месте" (без пересоздания DOM,
    // чтобы не потерять уже открытую переписку) и переводит всю историю
    // сообщений, кнопок-подсказок и кнопок-ссылок на новый язык.
    async _onSiteLanguageChanged(newLang) {
        const supported = ['ru', 'en', 'fr', 'el', 'ar'];
        if (!supported.includes(newLang) || newLang === this.lang) return;

        this.lang = newLang;
        localStorage.setItem(LANG_STORAGE_KEY, newLang);
        await this._loadTranslations();
        this._applyStaticTexts();
        await this._retranslateVisibleContent(newLang);
    }

    // ════════════════════════════════
    //  ОПРЕДЕЛЕНИЕ ЯЗЫКА
    // ════════════════════════════════
    _detectLang() {
        const supported = ['ru', 'en', 'fr', 'el', 'ar'];
        const stored = localStorage.getItem(LANG_STORAGE_KEY);
        const htmlLang = document.documentElement.lang;
        const lang = stored || htmlLang || 'ru';
        return supported.includes(lang) ? lang : 'ru';
    }

    async _loadTranslations() {
        try {
            const res = await fetch(`${I18N_BASE_PATH}${this.lang}.json`);
            this.t = await res.json();
        } catch { this.t = {}; }
    }

    _tr(key, fallback) { return this.t[key] || fallback; }

    _quickQuestions() {
        return [
            this._tr('chat_quick_1', FALLBACK_QUICK[0]),
            this._tr('chat_quick_2', FALLBACK_QUICK[1]),
            this._tr('chat_quick_3', FALLBACK_QUICK[2]),
            this._tr('chat_quick_4', FALLBACK_QUICK[3]),
        ];
    }

    // ════════════════════════════════
    //  РЕНДЕР
    // ════════════════════════════════
    _render() {
        const el = document.createElement('div');
        el.id = 'chat-widget';
        if (RTL_LANGS.includes(this.lang)) el.setAttribute('dir', 'rtl');
        el.innerHTML = `
      <button class="chat-toggle" id="chat-toggle" aria-label="${this._tr('chat_aria_toggle', 'Открыть чат')}">
        <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
        </svg>
        <span class="chat-pulse"></span>
      </button>
      <span class="chat-hint"><span class="hint-shine"></span><span class="chat-hint-text">${this._tr('chat_hint', '🦷 Задайте вопрос врачу')}</span></span>

      <div class="chat-window" id="chat-window">
        <div class="chat-header">
          <div class="chat-header-info">
            <div class="chat-avatar">🦷</div>
            <div>
              <div class="chat-header-name" id="chat-header-name">${this._tr('chat_header_name', 'Dental AI')}</div>
              <div class="chat-header-status" id="chat-header-status">${this._tr('chat_header_status', 'Онлайн · отвечает мгновенно')}</div>
            </div>
          </div>
          <button class="chat-tts-btn${this.ttsEnabled ? ' chat-tts-btn--on' : ''}" id="chat-tts-btn" type="button"
            aria-label="${this._tr('chat_aria_tts', 'Озвучивать ответы')}" title="${this._tr('chat_aria_tts', 'Озвучивать ответы')}">
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"/>
              <path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07"/>
            </svg>
          </button>
          <button class="chat-close-btn" id="chat-close-btn">✕</button>
          <div class="chat-header-wave"></div>
        </div>

        <div class="chat-messages" id="chat-messages"></div>

        <div class="chat-input-area">
          <input type="text" id="chat-input" class="chat-input"
            placeholder="${this._tr('chat_placeholder', 'Напишите вопрос...')}"
            maxlength="500" autocomplete="off"/>
          <button class="chat-mic" id="chat-mic" type="button" aria-pressed="false" aria-label="${this._tr('chat_aria_mic', 'Голосовой ввод')}" title="${this._tr('chat_aria_mic', 'Голосовой ввод')}">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/>
              <path d="M19 10v2a7 7 0 0 1-14 0v-2M12 19v4M8 23h8"/>
            </svg>
          </button>
          <button class="chat-send" id="chat-send" aria-label="${this._tr('chat_aria_send', 'Отправить')}">
            <svg width="19" height="19" viewBox="0 0 24 24" fill="currentColor">
              <path d="M2 21l21-9L2 3v7l15 2-15 2v7z"/>
            </svg>
          </button>
        </div>
      </div>`;
        document.body.appendChild(el);
    }

    _bindEvents() {
        document.getElementById('chat-toggle').addEventListener('click', () => this._toggleChat());
        document.getElementById('chat-close-btn').addEventListener('click', () => this._toggleChat(false));
        document.getElementById('chat-send').addEventListener('click', () => this._send());
        document.getElementById('chat-input').addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this._send(); }
        });
        document.getElementById('chat-mic')?.addEventListener('click', () => this._toggleMic());
        document.getElementById('chat-tts-btn')?.addEventListener('click', () => this._toggleTts());
    }

    // ════════════════════════════════
    //  ГОЛОСОВОЙ ВВОД (Web Speech API)
    // ════════════════════════════════
    _speechLangCode() {
        const map = { ru: 'ru-RU', en: 'en-US', fr: 'fr-FR', el: 'el-GR', ar: 'ar-SA' };
        return map[this.lang] || 'ru-RU';
    }

    _initVoiceRecognition() {
        const micBtn = document.getElementById('chat-mic');
        if (!micBtn) return;

        // Доступ к микрофону запрещён браузером на обычных HTTP-страницах
        // (исключение — localhost). Раньше SpeechRecognition молча возвращал
        // not-allowed, а обработчик тут же скрывал ошибку, поэтому кнопка
        // выглядела сломанной. Оставляем её видимой и объясняем причину по клику.
        if (window.isSecureContext === false) {
            this._markVoiceUnavailable('insecure');
            return;
        }

        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            // Кнопку не прячем: иначе пользователь не понимает, куда исчезла
            // заявленная функция. По клику показываем совместимый браузер.
            this._markVoiceUnavailable('unsupported');
            return;
        }

        this._recognition = new SpeechRecognition();
        this._recognition.continuous = false;
        this._recognition.interimResults = true; // показываем текст в поле по мере распознавания, не только в конце
        this._recognition.maxAlternatives = 1;

        this._recognition.addEventListener('start', () => {
            this._micStartPending = false;
            this._setListening(true);
        });

        this._recognition.addEventListener('result', e => {
            const input = document.getElementById('chat-input');
            if (!input) return;

            let finalText = '';
            let interimText = '';

            // e.resultIndex указывает только на изменившийся фрагмент. Если
            // собирать текст начиная с него, ранее распознанные слова исчезают
            // при следующем событии. Пересобираем полную текущую расшифровку.
            for (let i = 0; i < e.results.length; i++) {
                const transcript = e.results[i][0].transcript;
                if (e.results[i].isFinal) finalText += transcript;
                else interimText += transcript;
            }

            const spokenText = `${finalText}${interimText}`.trim();
            input.value = [this._voiceInputPrefix, spokenText].filter(Boolean).join(' ');

            if (finalText.trim()) {
                // Финальный (уточнённый) результат готов — текст остаётся в поле,
                // отправляет сам пользователь (кнопкой или Enter)
                input.focus();
            }
        });

        this._recognition.addEventListener('end', () => {
            this._micStartPending = false;
            this._setListening(false);
            document.getElementById('chat-input')?.focus();
        });

        this._recognition.addEventListener('error', event => {
            this._micStartPending = false;
            this._setListening(false);

            // stop(), вызванный самим пользователем, в некоторых реализациях
            // сопровождается aborted — это нормальное завершение, не ошибка.
            if (event.error === 'aborted' && this._micStoppedByUser) {
                this._micStoppedByUser = false;
                return;
            }

            this._showVoiceError(event.error);
        });
    }

    _toggleMic() {
        if (this._voiceUnavailableReason) {
            this._showVoiceError(this._voiceUnavailableReason);
            return;
        }

        if (!this._recognition || this._micStartPending) return;

        if (this._isListening) {
            this._micStoppedByUser = true;
            this._recognition.stop();
            return;
        }

        try {
            // Ставим тот же язык, что выбран на сайте — это заметно повышает
            // точность распознавания (движок не гадает между языками)
            this._recognition.lang = this._speechLangCode();
            this._voiceInputPrefix = document.getElementById('chat-input')?.value.trim() || '';
            this._micStoppedByUser = false;
            this._micStartPending = true;
            this._recognition.start();
        } catch (error) {
            this._micStartPending = false;
            this._setListening(false);
            this._showVoiceError(error?.name || 'start-failed');
        }
    }

    _markVoiceUnavailable(reason) {
        this._voiceUnavailableReason = reason;
        const micBtn = document.getElementById('chat-mic');
        if (!micBtn) return;

        micBtn.classList.add('chat-mic--unavailable');
        const message = this._voiceErrorMessage(reason);
        micBtn.setAttribute('aria-label', message);
        micBtn.setAttribute('title', message);
    }

    _voiceErrorMessage(reason) {
        const messages = {
            insecure: ['chat_voice_https_required', 'Голосовой ввод доступен только по защищённому HTTPS-соединению. Откройте сайт по HTTPS.'],
            unsupported: ['chat_voice_unsupported', 'Этот браузер не поддерживает распознавание речи. Откройте сайт в Chrome или Edge.'],
            'not-allowed': ['chat_voice_permission_denied', 'Доступ к микрофону запрещён. Разрешите его для сайта в настройках браузера и попробуйте снова.'],
            'service-not-allowed': ['chat_voice_permission_denied', 'Доступ к микрофону запрещён. Разрешите его для сайта в настройках браузера и попробуйте снова.'],
            'audio-capture': ['chat_voice_no_microphone', 'Микрофон не найден или занят другим приложением.'],
            network: ['chat_voice_network_error', 'Сервис распознавания речи недоступен. Проверьте интернет и попробуйте снова.'],
            'no-speech': ['chat_voice_no_speech', 'Речь не распознана. Нажмите микрофон и говорите чуть громче.']
        };
        const [key, fallback] = messages[reason] || ['chat_voice_start_error', 'Не удалось запустить голосовой ввод. Попробуйте ещё раз.'];
        return this._tr(key, fallback);
    }

    _showVoiceError(reason) {
        showError(this._voiceErrorMessage(reason), {
            title: this._tr('chat_aria_mic', 'Голосовой ввод')
        });
    }

    _setListening(on) {
        this._isListening = on;
        const micBtn = document.getElementById('chat-mic');
        if (!micBtn) return;

        micBtn.classList.toggle('chat-mic--active', on);
        micBtn.setAttribute('aria-pressed', on ? 'true' : 'false');

        const label = on
            ? this._tr('chat_voice_listening', 'Слушаю… Нажмите, чтобы остановить.')
            : this._voiceUnavailableReason
                ? this._voiceErrorMessage(this._voiceUnavailableReason)
                : this._tr('chat_aria_mic', 'Голосовой ввод');
        micBtn.setAttribute('aria-label', label);
        micBtn.setAttribute('title', label);
    }

    // ════════════════════════════════
    //  ОЗВУЧКА ОТВЕТОВ (Speech Synthesis)
    // ════════════════════════════════
    _toggleTts() {
        this.ttsEnabled = !this.ttsEnabled;
        localStorage.setItem('chat_tts_enabled', this.ttsEnabled ? '1' : '0');
        document.getElementById('chat-tts-btn')?.classList.toggle('chat-tts-btn--on', this.ttsEnabled);
        if (!this.ttsEnabled) this._stopSpeaking();
    }

    // getVoices() у части браузеров сразу пуст и заполняется асинхронно —
    // ждём событие voiceschanged, если список ещё не готов
    _loadVoices() {
        return new Promise(resolve => {
            const voices = window.speechSynthesis?.getVoices() || [];
            if (voices.length > 0) return resolve(voices);
            window.speechSynthesis.addEventListener('voiceschanged', () => {
                resolve(window.speechSynthesis.getVoices());
            }, { once: true });
            // Подстраховка — не все браузеры шлют событие вовремя
            setTimeout(() => resolve(window.speechSynthesis?.getVoices() || []), 1000);
        });
    }

    // Один голос на весь диалог, подобранный по качеству звучания, а не полу:
    // современные онлайн/neural-голоса (Google, Microsoft Online/Natural) звучат
    // куда естественнее и быстрее старых системных — их и ставим в приоритет.
    // Выбирается один раз и кэшируется, чтобы не "прыгать" между голосами.
    async _pickVoice() {
        if (this._cachedVoice) return this._cachedVoice;

        const voices = await this._loadVoices();
        if (voices.length === 0) return null;

        const langPrefix = this._speechLangCode().split('-')[0];
        const sameLang = voices.filter(v => v.lang?.toLowerCase().startsWith(langPrefix));
        const pool = sameLang.length > 0 ? sameLang : voices;

        const score = v => {
            const n = v.name.toLowerCase();
            if (n.includes('google')) return 3;              // онлайн-голоса Chrome — обычно самые живые
            if (/online|neural|natural|enhanced/.test(n)) return 2;
            if (v.localService === false) return 1;           // облачный голос — как правило лучше локального
            return 0;
        };

        this._cachedVoice = [...pool].sort((a, b) => score(b) - score(a))[0];
        return this._cachedVoice;
    }

    // Сбрасывает кэш голоса — пригодится, если захочешь снова подобрать голос
    // заново (например после смены языка сайта)
    _resetVoiceCache() {
        this._cachedVoice = null;
    }

    async _speak(text) {
        if (!this.ttsEnabled || !text) return;
        // Убираем эмодзи, markdown-звёздочки и ссылки-стрелки — читать их вслух не нужно
        const clean = text
            .replace(/\*\*(.*?)\*\*/g, '$1')
            .replace(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}]/gu, '')
            .replace(/→/g, '')
            .trim();
        if (!clean) return;

        this._stopSpeaking();

        // Сначала пробуем красивый нейросетевой голос (ElevenLabs через бэкенд).
        // Если на сервере не настроен API-ключ или запрос не удался — тихо
        // откатываемся на системный голос браузера, чтобы озвучка не пропадала.
        const played = await this._speakWithElevenLabs(clean);
        if (!played) this._speakWithBrowser(clean);
    }

    _stopSpeaking() {
        if (this._audioEl) {
            this._audioEl.pause();
            URL.revokeObjectURL(this._audioEl.src);
            this._audioEl = null;
        }
        window.speechSynthesis?.cancel();
    }

    async _speakWithElevenLabs(text) {
        try {
            const res = await fetch('/api/chat/tts', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ text })
            });
            if (!res.ok) return false; // 204 — ключ не настроен, 502 — сбой ElevenLabs

            const blob = await res.blob();
            if (!blob || blob.size === 0) return false;

            const url = URL.createObjectURL(blob);
            this._audioEl = new Audio(url);
            await this._audioEl.play();
            return true;
        } catch {
            return false;
        }
    }

    _speakWithBrowser(text) {
        if (!window.speechSynthesis) return;
        const utter = new SpeechSynthesisUtterance(text);
        utter.lang = this._speechLangCode();
        utter.rate = 1.12;

        this._pickVoice().then(voice => {
            if (voice) utter.voice = voice;
            window.speechSynthesis.speak(utter);
        });
    }

    // ════════════════════════════════
    //  ПРОАКТИВНЫЕ СООБЩЕНИЯ
    // ════════════════════════════════
    _initProactive() {
        if (this._proShown) return;
        const path = window.location.pathname;
        const trigger = PROACTIVE_TRIGGERS.find(t => path === t.path || path.endsWith(t.path));
        if (!trigger) return;

        this._proTimer = setTimeout(() => {
            if (this.isOpen) return;
            this._proShown = true;
            sessionStorage.setItem('chat_proactive_shown', '1');
            this._showPulse();
            this._toggleChat(true);
            // Небольшая задержка чтобы чат успел открыться
            setTimeout(() => this._sendProactive(trigger.msg), 600);
        }, trigger.delay);
    }

    _showPulse() {
        const p = document.querySelector('.chat-pulse');
        if (p) p.style.display = 'block';
    }

    async _sendProactive(msg) {
        this._showTyping();
        try {
            const res = await fetch('/api/chat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: msg, history: [], lang: this.lang, sessionId: this.sessionId })
            });
            const data = await res.json();
            this._hideTyping();
            this._addBotMessage(data.reply || '', data.suggestions || [], data.links || []);
        } catch {
            this._hideTyping();
        }
    }

    // ════════════════════════════════
    //  ОТКРЫТИЕ / ЗАКРЫТИЕ
    // ════════════════════════════════
    async _toggleChat(forceOpen = null) {
        this.isOpen = forceOpen !== null ? forceOpen : !this.isOpen;

        if (this.isOpen) {
            const cur = this._detectLang();
            if (cur !== this.lang) {
                await this._onSiteLanguageChanged(cur);
            }
            if (this._proTimer) { clearTimeout(this._proTimer); this._proTimer = null; }
        } else if ((this._isListening || this._micStartPending) && this._recognition) {
            // Не оставляем микрофон активным после закрытия окна чата.
            this._micStoppedByUser = true;
            try {
                if (typeof this._recognition.abort === 'function') this._recognition.abort();
                else this._recognition.stop();
            }
            catch { this._micStartPending = false; this._setListening(false); }
        }

        document.getElementById('chat-widget').classList.toggle('chat-open', this.isOpen);
        document.getElementById('chat-window').classList.toggle('chat-window--visible', this.isOpen);
        document.body.classList.toggle('chat-is-open', this.isOpen);

        if (this.isOpen && this.history.length === 0) {
            this._showWelcome();
            setTimeout(() => document.getElementById('chat-input').focus(), 350);
        }
    }

    _applyStaticTexts() {
        const w = document.getElementById('chat-widget');
        if (RTL_LANGS.includes(this.lang)) w.setAttribute('dir', 'rtl');
        else w.removeAttribute('dir');

        const n = document.getElementById('chat-header-name');
        const s = document.getElementById('chat-header-status');
        const hintText = document.querySelector('.chat-hint-text');
        const toggleBtn = document.getElementById('chat-toggle');
        const ttsBtn = document.getElementById('chat-tts-btn');
        const micBtn = document.getElementById('chat-mic');
        const sendBtn = document.getElementById('chat-send');

        if (n) n.textContent = this._tr('chat_header_name', 'Dental AI');
        if (s) s.textContent = this._tr('chat_header_status', 'Онлайн · отвечает мгновенно');
        if (hintText) hintText.textContent = this._tr('chat_hint', '🦷 Задайте вопрос врачу');
        if (toggleBtn) toggleBtn.setAttribute('aria-label', this._tr('chat_aria_toggle', 'Открыть чат'));
        if (sendBtn) sendBtn.setAttribute('aria-label', this._tr('chat_aria_send', 'Отправить'));
        if (ttsBtn) {
            const label = this._tr('chat_aria_tts', 'Озвучивать ответы');
            ttsBtn.setAttribute('aria-label', label);
            ttsBtn.setAttribute('title', label);
        }
        if (micBtn) {
            const label = this._voiceUnavailableReason
                ? this._voiceErrorMessage(this._voiceUnavailableReason)
                : this._tr('chat_aria_mic', 'Голосовой ввод');
            micBtn.setAttribute('aria-label', label);
            micBtn.setAttribute('title', label);
        }

        this._applyInputPlaceholder();
    }

    // Плейсхолдер поля ввода зависит от того, на каком шаге "умной записи"
    // сейчас находится диалог — если запись не идёт, показываем обычный.
    _applyInputPlaceholder() {
        const i = document.getElementById('chat-input');
        if (!i) return;
        const stepKeys = {
            ask_name: ['chat_booking_name_placeholder', 'Введите ваше имя...'],
            ask_phone: ['chat_booking_phone_placeholder', '+7 (999) 999-99-99'],
            ask_date: ['chat_booking_date_placeholder', 'Например: 15 июля, после 14:00'],
            ask_comment: ['chat_booking_comment_placeholder', 'Опишите кратко...'],
        };
        const step = this.booking?.step;
        const [key, fallback] = stepKeys[step] || ['chat_placeholder', 'Напишите вопрос...'];
        i.placeholder = this._tr(key, fallback);
    }

    _showWelcome() {
        const name = sessionStorage.getItem('patientName');
        const welcome = name
            ? this._tr('chat_welcome_named', 'Добро пожаловать, {name}! 👋 Я Дента — AI-ассистент Dental Clinic. Чем могу помочь?').replace('{name}', name)
            : this._tr('chat_welcome', 'Привет! 👋 Я Дента — AI-ассистент Dental Clinic. Знаю всё об услугах, ценах и врачах.');
        this._addBotMessage(welcome, this._quickQuestions(), []);
    }

    // ════════════════════════════════
    //  ОТПРАВКА СООБЩЕНИЯ
    // ════════════════════════════════
    async _send() {
        const input = document.getElementById('chat-input');
        const text = input.value.trim();
        if (!text || this.isTyping) return;
        input.value = '';
        this._removeSuggestions();

        // Если идёт диалог записи — обрабатываем шаг
        if (this.booking.step !== 'idle' && this.booking.step !== 'done') {
            this._addUserMessage(text);
            this._handleBookingStep(text);
            return;
        }

        await this._sendText(text);
    }

    async _sendText(text) {
        this._addUserMessage(text);
        this.history.push({ role: 'user', text });
        this._showTyping();

        const streamed = await this._sendTextStreaming(text);
        if (!streamed) await this._sendTextFallback(text);
    }

    // Стриминг через SSE: ответ печатается по мере генерации, а не появляется
    // одним куском после того как модель полностью его сгенерировала.
    // Возвращает false, если стриминг не удался — тогда вызывающий код
    // повторяет запрос через обычный /api/chat.
    async _sendTextStreaming(text) {
        let res;
        try {
            res = await fetch('/api/chat/stream', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    message: text,
                    history: this.history.slice(-10),
                    lang: this.lang,
                    sessionId: this.sessionId,
                    patientId: sessionStorage.getItem('patientId') ? parseInt(sessionStorage.getItem('patientId')) : null
                })
            });
        } catch {
            return false;
        }

        if (!res.ok || !res.body) return false;

        this._hideTyping();

        let bubbleText = document.createElement('div');
        let bubbleEl = null;
        let fullReply = '';
        let gotAnyDelta = false;
        let buffer = '';

        const ensureBubble = () => {
            if (bubbleEl) return;
            const messages = document.getElementById('chat-messages');
            bubbleEl = document.createElement('div');
            bubbleEl.className = 'chat-bubble chat-bubble--bot';
            bubbleEl.innerHTML = `
          <div class="chat-bubble-inner">
            <span class="chat-bubble-avatar">🦷</span>
            <div class="chat-bubble-text"></div>
          </div>
          <div class="chat-bubble-time">${this._time()}</div>`;
            messages.appendChild(bubbleEl);
            bubbleText = bubbleEl.querySelector('.chat-bubble-text');
        };

        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        let finalData = null;

        try {
            while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                buffer += decoder.decode(value, { stream: true });

                let sepIdx;
                while ((sepIdx = buffer.indexOf('\n\n')) !== -1) {
                    const rawEvent = buffer.slice(0, sepIdx);
                    buffer = buffer.slice(sepIdx + 2);
                    const line = rawEvent.split('\n').find(l => l.startsWith('data:'));
                    if (!line) continue;

                    let payload;
                    try { payload = JSON.parse(line.slice(5).trim()); } catch { continue; }

                    if (payload.error) {
                        this._hideTyping();
                        if (!gotAnyDelta) return false; // ошибка до старта — пусть сработает фолбэк
                        this._addBotMessage(payload.error, [], []);
                        return true;
                    }

                    if (payload.delta) {
                        gotAnyDelta = true;
                        ensureBubble();
                        fullReply += payload.delta;
                        bubbleText.innerHTML = this._formatWithPrices(fullReply);
                        const messages = document.getElementById('chat-messages');
                        messages.scrollTop = messages.scrollHeight;
                    }

                    if (payload.done) {
                        finalData = payload;
                    }
                }
            }
        } catch {
            this._hideTyping();
            return gotAnyDelta; // если что-то уже напечатали — не дублируем через фолбэк
        }

        if (!gotAnyDelta) return false; // ничего не пришло — пробуем обычный запрос

        const suggestions = finalData?.suggestions || [];
        const links = finalData?.links || [];
        const wantsBooking = finalData?.startBooking === true
            || suggestions.some(s => this._isBookingIntent(s));

        this._renderLinksAndSuggestions(links, suggestions);
        this._speak(fullReply);

        if (wantsBooking && this.booking.step === 'idle') this._addBookingStartButton();

        this.history.push({ role: 'bot', text: fullReply });
        return true;
    }

    // Резервный путь без стриминга — используется, если браузер/сеть не
    // поддерживают SSE (например, некоторые корпоративные прокси буферизуют
    // ответ целиком). Логика идентична прежней нестриминговой версии.
    async _sendTextFallback(text) {
        this._showTyping();
        try {
            const res = await fetch('/api/chat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    message: text,
                    history: this.history.slice(-10),
                    lang: this.lang,
                    sessionId: this.sessionId,
                    patientId: sessionStorage.getItem('patientId') ? parseInt(sessionStorage.getItem('patientId')) : null
                })
            });
            const data = await res.json();
            this._hideTyping();

            const reply = data.reply || this._tr('chat_error_generic', 'Извините, произошла ошибка.');
            const suggestions = data.suggestions || [];
            const links = data.links || [];

            const wantsBooking = data.startBooking === true
                || suggestions.some(s => this._isBookingIntent(s));

            this._addBotMessage(reply, suggestions, links);

            if (wantsBooking && this.booking.step === 'idle') {
                this._addBookingStartButton();
            }

            this.history.push({ role: 'bot', text: reply });
        } catch {
            this._hideTyping();
            this._addBotMessage(this._tr('chat_error_connection', '⚠️ Нет соединения. Проверьте интернет и попробуйте снова.'), [], []);
        }
    }

    // Достраивает кнопки-ссылки и кнопки-подсказки под уже напечатанным
    // потоковым сообщением (само сообщение уже в DOM, в отличие от _addBotMessage)
    _renderLinksAndSuggestions(links, suggestions) {
        const messages = document.getElementById('chat-messages');

        if (links && links.length > 0) {
            const linksWrap = document.createElement('div');
            linksWrap.className = 'chat-links';
            links.forEach(link => {
                const a = document.createElement('a');
                a.className = 'chat-link-btn';
                a.href = link.url;
                a.dataset.origText = link.text;
                a.dataset.origLang = this.lang;
                a.innerHTML = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="M5 12h14M12 5l7 7-7 7"/></svg> <span class="chat-link-label">${this._escapeHtml(link.text)}</span>`;
                linksWrap.appendChild(a);
            });
            messages.appendChild(linksWrap);
        }

        if (suggestions && suggestions.length > 0) {
            const wrap = document.createElement('div');
            wrap.className = 'chat-suggestions';
            suggestions.forEach(s => {
                const btn = document.createElement('button');
                btn.className = 'chat-suggestion-btn';
                btn.textContent = s;
                btn.dataset.origText = s;
                btn.dataset.origLang = this.lang;
                btn.addEventListener('click', () => {
                    this._removeSuggestions();
                    if (this._isBookingIntent(s)) {
                        this._startBooking();
                    } else {
                        this._sendText(s);
                    }
                });
                wrap.appendChild(btn);
            });
            messages.appendChild(wrap);
        }

        messages.scrollTop = messages.scrollHeight;
    }

    // Единая проверка "это подсказка про запись на приём?" — работает
    // независимо от текущего языка сайта (ru/en/fr/el/ar)
    _isBookingIntent(text) {
        return /записат|приём|record|rendez|ραντεβού|حجز/i.test(text);
    }

    // ════════════════════════════════
    //  УМНАЯ ЗАПИСЬ
    // ════════════════════════════════
    _addBookingStartButton() {
        const messages = document.getElementById('chat-messages');
        const wrap = document.createElement('div');
        wrap.className = 'chat-booking-start';
        wrap.innerHTML = `
      <button class="chat-booking-btn" id="chat-start-booking">
        ${this._tr('chat_booking_start_btn', '📅 Записаться на приём прямо здесь')}
      </button>`;
        messages.appendChild(wrap);
        messages.scrollTop = messages.scrollHeight;

        document.getElementById('chat-start-booking').addEventListener('click', () => {
            wrap.remove();
            this._startBooking();
        });
    }

    _startBooking() {
        const patientId = sessionStorage.getItem('patientId');
        const patientName = sessionStorage.getItem('patientName');

        if (patientId) {
            // Авторизованный пациент — предлагаем перейти в ЛК
            const guestLabel = this._tr('chat_booking_auth_guest', 'Записаться здесь как гость');
            this._addBotMessage(
                this._tr('chat_booking_auth_intro', 'Вы авторизованы как **{name}** ✅\nВы можете создать заявку прямо в личном кабинете — там удобнее отслеживать статус.').replace('{name}', patientName),
                [this._tr('chat_booking_auth_create', 'Создать заявку в ЛК'), guestLabel],
                [{ text: this._tr('chat_booking_auth_link', 'Личный кабинет →'), url: '/pages/patient-dashboard.html' }]
            );
            // Кнопка гостя сравнивается по значению из словаря текущего языка,
            // а не по русской подстроке — работает на любом языке
            const last = document.querySelector('.chat-suggestions:last-child');
            if (last) {
                const btns = last.querySelectorAll('.chat-suggestion-btn');
                btns.forEach(btn => {
                    if (btn.textContent === guestLabel) {
                        btn.addEventListener('click', () => {
                            last.remove();
                            this._beginBookingDialog();
                        }, { once: true });
                    }
                });
            }
        } else {
            this._beginBookingDialog();
        }
    }

    _beginBookingDialog() {
        this.booking = { step: 'ask_name', name: '', phone: '', date: '', comment: '' };
        this._addBotMessage(this._tr('chat_booking_intro', 'Отлично! 😊 Давайте оформим заявку.\n\nКак вас зовут?'), [], []);
        document.getElementById('chat-input').placeholder = this._tr('chat_booking_name_placeholder', 'Введите ваше имя...');
    }

    _handleBookingStep(text) {
        const step = this.booking.step;

        if (step === 'ask_name') {
            this.booking.name = text;
            this.booking.step = 'ask_phone';
            this._addBotMessage(this._tr('chat_booking_phone_ask', 'Приятно познакомиться, **{name}**! 👋\n\nУкажите ваш номер телефона — администратор перезвонит для подтверждения.').replace('{name}', text), [], []);
            document.getElementById('chat-input').placeholder = this._tr('chat_booking_phone_placeholder', '+7 (999) 999-99-99');

        } else if (step === 'ask_phone') {
            if (!/[\d\s\-\+\(\)]{7,}/.test(text)) {
                this._addBotMessage(this._tr('chat_booking_phone_invalid', 'Пожалуйста, укажите корректный номер телефона 📞'), [], []);
                return;
            }
            this.booking.phone = text;
            this.booking.step = 'ask_date';
            this._addBotMessage(this._tr('chat_booking_date_ask', 'Отлично! 📞\n\nУкажите удобную дату визита (например: 15 июля или оставьте пустым — мы сами предложим время).'), [], []);
            document.getElementById('chat-input').placeholder = this._tr('chat_booking_date_placeholder', 'Например: 15 июля, после 14:00');

        } else if (step === 'ask_date') {
            this.booking.date = text || this._tr('chat_booking_date_empty', 'Не указано');
            this.booking.step = 'ask_comment';
            this._addBotMessage(this._tr('chat_booking_comment_ask', 'Что вас беспокоит или какую процедуру хотите? (необязательно — можно написать "не знаю")'), [], []);
            document.getElementById('chat-input').placeholder = this._tr('chat_booking_comment_placeholder', 'Опишите кратко...');

        } else if (step === 'ask_comment') {
            this.booking.comment = text;
            this.booking.step = 'confirm';
            this._showBookingConfirmation();
        }
    }

    _showBookingConfirmation() {
        const b = this.booking;
        const messages = document.getElementById('chat-messages');

        const card = document.createElement('div');
        card.className = 'chat-booking-card';
        card.innerHTML = `
      <div class="chat-booking-title">${this._tr('chat_booking_confirm_title', '📋 Проверьте данные заявки')}</div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_name', 'Имя:')}</span><strong>${b.name}</strong></div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_phone', 'Телефон:')}</span><strong>${b.phone}</strong></div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_date', 'Дата:')}</span><strong>${b.date}${this._parseDate(b.date) ? '' : this._tr('chat_booking_date_tbc', ' (уточним при звонке)')}</strong></div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_comment', 'Комментарий:')}</span><strong>${b.comment}</strong></div>
      <div class="chat-booking-actions">
        <button class="chat-booking-confirm" id="book-yes">${this._tr('chat_booking_btn_confirm', '✅ Данные верны, отправить')}</button>
        <button class="chat-booking-cancel"  id="book-no">${this._tr('chat_booking_btn_edit', '✏️ Изменить данные')}</button>
      </div>`;

        messages.appendChild(card);
        messages.scrollTop = messages.scrollHeight;

        document.getElementById('book-yes').addEventListener('click', () => {
            card.remove();
            this._submitBooking();
        });
        document.getElementById('book-no').addEventListener('click', () => {
            card.remove();
            this.booking.step = 'idle';
            this._addBotMessage(this._tr('chat_booking_restart', 'Хорошо, давайте начнём заново. Как вас зовут?'), [], []);
            this.booking.step = 'ask_name';
            document.getElementById('chat-input').placeholder = this._tr('chat_booking_name_placeholder', 'Введите ваше имя...');
        });
    }

    // Парсит свободный текст даты → ISO строку для бэкенда
    // Понимает: "15 июля", "15.07", "15/07/2026", "завтра", числа
    _parseDate(text) {
        if (!text || text === this._tr('chat_booking_date_empty', 'Не указано')) return null;

        // Названия месяцев на всех поддерживаемых языках сайта — иначе
        // "15 July" или "15 juillet" не распознавались бы, только русские
        const months = {
            'январ': 1, 'феврал': 2, 'март': 3, 'апрел': 4, 'май': 5, 'мая': 5,
            'июн': 6, 'июл': 7, 'август': 8, 'сентябр': 9, 'октябр': 10,
            'ноябр': 11, 'декабр': 12,
            // English
            'jan': 1, 'feb': 2, 'mar': 3, 'apr': 4, 'may': 5, 'jun': 6,
            'jul': 7, 'aug': 8, 'sep': 9, 'oct': 10, 'nov': 11, 'dec': 12,
            // Français
            'janv': 1, 'févr': 2, 'fevr': 2, 'mars': 3, 'avr': 4, 'mai': 5, 'juin': 6,
            'juil': 7, 'août': 8, 'aout': 8, 'sept': 9, 'oct': 10, 'nov': 11, 'déc': 12, 'dec': 12,
            // Ελληνικά
            'ιανουάρ': 1, 'φεβρουάρ': 2, 'μάρτ': 3, 'απρίλ': 4, 'μάι': 5, 'μαΐ': 5,
            'ιούνι': 6, 'ιούλι': 7, 'αύγουστ': 8, 'σεπτέμβρ': 9, 'οκτώβρ': 10,
            'νοέμβρ': 11, 'δεκέμβρ': 12,
            // العربية
            'يناير': 1, 'فبراير': 2, 'مارس': 3, 'أبريل': 4, 'ابريل': 4, 'مايو': 5,
            'يونيو': 6, 'يوليو': 7, 'أغسطس': 8, 'اغسطس': 8, 'سبتمبر': 9,
            'أكتوبر': 10, 'اكتوبر': 10, 'نوفمبر': 11, 'ديسمبر': 12,
        };

        const now = new Date();
        const year = now.getFullYear();

        // "завтра" / "tomorrow" / "demain" / "αύριο" / "غدًا"
        if (/завтра|tomorrow|demain|αύριο|غد/i.test(text)) {
            const d = new Date(now);
            d.setDate(d.getDate() + 1);
            return d.toISOString().split('T')[0] + 'T00:00:00';
        }

        // "15 июля" / "July 15" / "15 juillet" и т.п. — день и название месяца
        // в любом порядке, буквы любого поддерживаемого алфавита
        const monthNameMatch = text.match(/(\d{1,2})\s+([a-zA-Zа-яёА-ЯЁάέήίόύώΆΈΉΊΌΎΏα-ωΑ-Ωء-ي]+)|([a-zA-Zа-яёА-ЯЁάέήίόύώΆΈΉΊΌΎΏα-ωΑ-Ωء-ي]+)\s+(\d{1,2})/i);
        if (monthNameMatch) {
            const day = parseInt(monthNameMatch[1] || monthNameMatch[4]);
            const monthWord = (monthNameMatch[2] || monthNameMatch[3] || '').toLowerCase();
            const monthKey = Object.keys(months).find(k => monthWord.startsWith(k));
            if (monthKey) {
                const month = months[monthKey];
                const yearMatch = text.match(/(\d{4})/);
                const y = yearMatch ? parseInt(yearMatch[1]) : year;
                return `${y}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}T00:00:00`;
            }
        }

        // "15.07" или "15.07.2026" или "15/07/2026"
        const numMatch = text.match(/(\d{1,2})[./](\d{1,2})(?:[./](\d{4}))?/);
        if (numMatch) {
            const day = numMatch[1].padStart(2, '0');
            const month = numMatch[2].padStart(2, '0');
            const y = numMatch[3] || year;
            return `${y}-${month}-${day}T00:00:00`;
        }

        // Не удалось распарсить — возвращаем null (дата не указана)
        return null;
    }

    async _submitBooking() {
        this._addBotMessage(this._tr('chat_booking_sending', 'Отправляю заявку...⏳'), [], []);

        try {
            const b = this.booking;
            const pid = sessionStorage.getItem('patientId');
            const token = sessionStorage.getItem('authToken');

            // Парсим дату из текста пользователя → ISO формат для бэкенда
            const parsedDate = this._parseDate(b.date);
            const noComment = this._tr('chat_booking_no_comment', 'не знаю');
            const viaChatTag = this._tr('chat_booking_via_chat_tag', '[Заявка через чат]');

            const res = await fetch('/api/AppointmentRequest', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(token ? { 'Authorization': `Bearer ${token}` } : {})
                },
                body: JSON.stringify({
                    firstName: b.name,
                    phone: b.phone,
                    appointmentDate: parsedDate,
                    comment: b.comment && b.comment.toLowerCase() !== noComment.toLowerCase() ? b.comment + ' ' + viaChatTag : viaChatTag,
                    patientId: pid ? parseInt(pid) : null,
                    status: 'pending'
                })
            });

            if (res.ok) {
                this.booking.step = 'done';
                document.getElementById('chat-input').placeholder = this._tr('chat_placeholder', 'Напишите вопрос...');
                this._addBotMessage(
                    this._tr('chat_booking_success', '✅ Заявка успешно отправлена!\n\nАдминистратор перезвонит вам в ближайшее время для подтверждения записи. Обычно это занимает до 2 часов в рабочее время (Пн–Сб 9:00–20:00).'),
                    [this._tr('chat_booking_ask_more', 'Задать ещё вопрос'), this._tr('chat_booking_ask_services', 'Узнать о наших услугах')],
                    [{ text: this._tr('chat_booking_contact_link', 'Контакты клиники →'), url: '/pages/contact.html' }]
                );
                this.booking = { step: 'done', name: '', phone: '', date: '', comment: '' };
            } else {
                throw new Error('server error');
            }
        } catch {
            this.booking.step = 'idle';
            this._addBotMessage(this._tr('chat_booking_error', '⚠️ Не удалось отправить заявку. Пожалуйста, позвоните нам: **+7 (499) 999-99-99**'), [], [{ text: this._tr('chat_booking_contact_link', 'Контакты клиники →'), url: '/pages/contact.html' }]);
        }
    }

    // ════════════════════════════════
    //  РЕНДЕР СООБЩЕНИЙ
    // ════════════════════════════════
    _addUserMessage(text) {
        const messages = document.getElementById('chat-messages');
        const bubble = document.createElement('div');
        bubble.className = 'chat-bubble chat-bubble--user';
        bubble.dataset.origText = text;
        bubble.dataset.origLang = this.lang;
        bubble.innerHTML = `
      <div class="chat-bubble-inner">
        <div class="chat-bubble-text">${this._format(text)}</div>
      </div>
      <div class="chat-bubble-time">${this._time()}</div>`;
        messages.appendChild(bubble);
        messages.scrollTop = messages.scrollHeight;
    }

    _addBotMessage(text, suggestions = [], links = []) {
        const messages = document.getElementById('chat-messages');

        const bubble = document.createElement('div');
        bubble.className = 'chat-bubble chat-bubble--bot';
        bubble.dataset.origText = text;
        bubble.dataset.origLang = this.lang;
        bubble.innerHTML = `
      <div class="chat-bubble-inner">
        <span class="chat-bubble-avatar">🦷</span>
        <div class="chat-bubble-text">${this._formatWithPrices(text)}</div>
      </div>
      <div class="chat-bubble-time">${this._time()}</div>`;
        messages.appendChild(bubble);
        this._speak(text);

        // Кнопки-ссылки
        if (links && links.length > 0) {
            const linksWrap = document.createElement('div');
            linksWrap.className = 'chat-links';
            links.forEach(link => {
                const a = document.createElement('a');
                a.className = 'chat-link-btn';
                a.href = link.url;
                a.dataset.origText = link.text;
                a.dataset.origLang = this.lang;
                a.innerHTML = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="M5 12h14M12 5l7 7-7 7"/></svg> <span class="chat-link-label">${this._escapeHtml(link.text)}</span>`;
                linksWrap.appendChild(a);
            });
            messages.appendChild(linksWrap);
        }

        // Кнопки-подсказки
        if (suggestions && suggestions.length > 0) {
            const wrap = document.createElement('div');
            wrap.className = 'chat-suggestions';
            suggestions.forEach(s => {
                const btn = document.createElement('button');
                btn.className = 'chat-suggestion-btn';
                btn.textContent = s;
                btn.dataset.origText = s;
                btn.dataset.origLang = this.lang;
                btn.addEventListener('click', () => {
                    this._removeSuggestions();
                    if (this._isBookingIntent(s)) {
                        this._startBooking();
                    } else {
                        this._sendText(s);
                    }
                });
                wrap.appendChild(btn);
            });
            messages.appendChild(wrap);
        }

        messages.scrollTop = messages.scrollHeight;
    }

    // ════════════════════════════════
    //  ПЕРЕВОД ИСТОРИИ ПРИ СМЕНЕ ЯЗЫКА
    // ════════════════════════════════
    // Каждое сообщение/кнопка хранит оригинальный текст и язык, на котором
    // оно было впервые показано (data-orig-text / data-orig-lang). При смене
    // языка сайта переводим заново ВСЕГДА от оригинала, а не от того, что
    // сейчас на экране — так переключение туда-обратно не "портит" текст
    // повторными переводами и всегда возвращает точную исходную формулировку,
    // если пользователь вернулся на язык, на котором сообщение появилось.
    async _retranslateVisibleContent(newLang) {
        const jobs = [];

        document.querySelectorAll('#chat-messages .chat-bubble[data-orig-text]').forEach(bubble => {
            jobs.push(this._retranslateBubble(bubble, newLang));
        });
        document.querySelectorAll('.chat-suggestion-btn[data-orig-text]').forEach(btn => {
            jobs.push(this._retranslateInto(btn, newLang, (val) => { btn.textContent = val; }));
        });
        document.querySelectorAll('.chat-link-btn[data-orig-text]').forEach(a => {
            const label = a.querySelector('.chat-link-label');
            if (label) jobs.push(this._retranslateInto(a, newLang, (val) => { label.textContent = val; }));
        });

        // Кнопка "Записаться на приём", если сейчас видна
        const startBtn = document.getElementById('chat-start-booking');
        if (startBtn) startBtn.textContent = this._tr('chat_booking_start_btn', startBtn.textContent);

        // Подписи и кнопки карточки подтверждения записи, если она открыта —
        // сами введённые пользователем данные (имя/телефон/дату/комментарий)
        // не трогаем, переводим только подписи полей и кнопки
        const card = document.querySelector('.chat-booking-card');
        if (card) {
            const title = card.querySelector('.chat-booking-title');
            if (title) title.textContent = this._tr('chat_booking_confirm_title', title.textContent);
            const labelKeys = ['chat_booking_label_name', 'chat_booking_label_phone', 'chat_booking_label_date', 'chat_booking_label_comment'];
            card.querySelectorAll('.chat-booking-row span').forEach((span, i) => {
                if (labelKeys[i]) span.textContent = this._tr(labelKeys[i], span.textContent);
            });
            const yes = document.getElementById('book-yes');
            if (yes) yes.textContent = this._tr('chat_booking_btn_confirm', yes.textContent);
            const no = document.getElementById('book-no');
            if (no) no.textContent = this._tr('chat_booking_btn_edit', no.textContent);
        }

        await Promise.all(jobs);
    }

    async _retranslateBubble(bubble, newLang) {
        const original = bubble.dataset.origText;
        const originalLang = bubble.dataset.origLang || 'ru';
        const textEl = bubble.querySelector('.chat-bubble-text');
        if (!original || !textEl) return;

        const display = newLang === originalLang ? original : await translateText(original, newLang, 'text', { assumeRussianSource: false });
        const isBot = bubble.classList.contains('chat-bubble--bot');
        textEl.innerHTML = isBot ? this._formatWithPrices(display) : this._format(display);
    }

    async _retranslateInto(el, newLang, apply) {
        const original = el.dataset.origText;
        const originalLang = el.dataset.origLang || 'ru';
        if (!original) return;
        const display = newLang === originalLang ? original : await translateText(original, newLang, 'text', { assumeRussianSource: false });
        apply(display);
    }

    _removeSuggestions() {
        document.querySelectorAll('.chat-suggestions, .chat-links, .chat-booking-start').forEach(el => el.remove());
    }

    _showTyping() {
        this.isTyping = true;
        const messages = document.getElementById('chat-messages');
        const el = document.createElement('div');
        el.id = 'chat-typing';
        el.className = 'chat-bubble chat-bubble--bot';
        el.innerHTML = `
      <div class="chat-bubble-inner">
        <span class="chat-bubble-avatar">🦷</span>
        <div class="chat-typing-dots"><span></span><span></span><span></span></div>
      </div>`;
        messages.appendChild(el);
        messages.scrollTop = messages.scrollHeight;
    }

    _hideTyping() {
        this.isTyping = false;
        document.getElementById('chat-typing')?.remove();
    }

    _escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text ?? '';
        return div.innerHTML;
    }

    _format(text) {
        return this._escapeHtml(text)
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            .replace(/\n/g, '<br>');
    }

    _formatWithPrices(text) {
        let f = this._format(text);
        f = f.replace(/(\d[\d\s]*(?:₽|руб\.?|р\.?))/g, '<span class="chat-price">$1</span>');
        return f;
    }

    _time() {
        const locales = { ru: 'ru', en: 'en', fr: 'fr', el: 'el', ar: 'ar' };
        return new Date().toLocaleTimeString(locales[this.lang] || 'ru', { hour: '2-digit', minute: '2-digit' });
    }
}

export { ChatBot };
