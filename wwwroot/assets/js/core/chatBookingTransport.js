export function buildChatAppointmentPayload(booking, { parsedDate, noComment, viaChatTag } = {}) {
    const rawComment = booking?.comment ?? '';
    const meaningfulComment = rawComment
        && rawComment.toLowerCase() !== String(noComment ?? '').toLowerCase();

    return {
        firstName: booking?.name ?? '',
        phone: booking?.phone ?? '',
        appointmentDate: parsedDate ?? null,
        comment: meaningfulComment ? `${rawComment} ${viaChatTag}` : viaChatTag
    };
}

export function escapeChatBookingHtml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
}

export async function submitChatAppointment(payload, { fetchImpl = fetch } = {}) {
    return fetchImpl('/api/AppointmentRequest', {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
    });
}

// ChatBot historically assembled a bearer header from sessionStorage and also sent
// patientId/status in the request body. Authentication now lives in the HttpOnly
// dc_auth cookie, while identity and workflow status are server-owned fields.
// Keep that security boundary in one small transport module instead of duplicating
// authentication logic in the chat UI.
export function installChatBookingCookieTransport(ChatBotClass) {
    if (!ChatBotClass?.prototype) return;

    // The legacy confirmation card interpolated user-entered name/phone/date/comment
    // directly into innerHTML. Escape every user-controlled field before rendering so
    // the booking flow cannot become a DOM-XSS sink.
    ChatBotClass.prototype._showBookingConfirmation = function () {
        const booking = this.booking;
        const messages = document.getElementById('chat-messages');
        if (!messages) return;

        const safeName = escapeChatBookingHtml(booking.name);
        const safePhone = escapeChatBookingHtml(booking.phone);
        const safeDate = escapeChatBookingHtml(booking.date);
        const safeComment = escapeChatBookingHtml(booking.comment);
        const dateSuffix = this._parseDate(booking.date)
            ? ''
            : escapeChatBookingHtml(this._tr('chat_booking_date_tbc', ' (уточним при звонке)'));

        const card = document.createElement('div');
        card.className = 'chat-booking-card';
        card.innerHTML = `
      <div class="chat-booking-title">${this._tr('chat_booking_confirm_title', '📋 Проверьте данные заявки')}</div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_name', 'Имя:')}</span><strong>${safeName}</strong></div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_phone', 'Телефон:')}</span><strong>${safePhone}</strong></div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_date', 'Дата:')}</span><strong>${safeDate}${dateSuffix}</strong></div>
      <div class="chat-booking-row"><span>${this._tr('chat_booking_label_comment', 'Комментарий:')}</span><strong>${safeComment}</strong></div>
      <div class="chat-booking-actions">
        <button class="chat-booking-confirm" id="book-yes">${this._tr('chat_booking_btn_confirm', '✅ Данные верны, отправить')}</button>
        <button class="chat-booking-cancel" id="book-no">${this._tr('chat_booking_btn_edit', '✏️ Изменить данные')}</button>
      </div>`;

        messages.appendChild(card);
        messages.scrollTop = messages.scrollHeight;

        card.querySelector('#book-yes')?.addEventListener('click', () => {
            card.remove();
            this._submitBooking();
        });
        card.querySelector('#book-no')?.addEventListener('click', () => {
            card.remove();
            this.booking.step = 'idle';
            this._addBotMessage(
                this._tr('chat_booking_restart', 'Хорошо, давайте начнём заново. Как вас зовут?'),
                [],
                []
            );
            this.booking.step = 'ask_name';
            const input = document.getElementById('chat-input');
            if (input)
                input.placeholder = this._tr('chat_booking_name_placeholder', 'Введите ваше имя...');
        });
    };

    ChatBotClass.prototype._submitBooking = async function () {
        this._addBotMessage(
            this._tr('chat_booking_sending', 'Отправляю заявку...⏳'),
            [],
            []
        );

        try {
            const booking = this.booking;
            const parsedDate = this._parseDate(booking.date);
            const noComment = this._tr('chat_booking_no_comment', 'не знаю');
            const viaChatTag = this._tr('chat_booking_via_chat_tag', '[Заявка через чат]');

            const payload = buildChatAppointmentPayload(booking, {
                parsedDate,
                noComment,
                viaChatTag
            });

            const response = await submitChatAppointment(payload);
            if (!response.ok) throw new Error(`appointment create failed: ${response.status}`);

            this.booking.step = 'done';
            const input = document.getElementById('chat-input');
            if (input)
                input.placeholder = this._tr('chat_placeholder', 'Напишите вопрос...');

            this._addBotMessage(
                this._tr(
                    'chat_booking_success',
                    '✅ Заявка успешно отправлена!\n\nАдминистратор перезвонит вам в ближайшее время для подтверждения записи. Обычно это занимает до 2 часов в рабочее время (Пн–Сб 9:00–20:00).'
                ),
                [
                    this._tr('chat_booking_ask_more', 'Задать ещё вопрос'),
                    this._tr('chat_booking_ask_services', 'Узнать о наших услугах')
                ],
                [{
                    text: this._tr('chat_booking_contact_link', 'Контакты клиники →'),
                    url: '/pages/contact.html'
                }]
            );

            this.booking = { step: 'done', name: '', phone: '', date: '', comment: '' };
        } catch {
            this.booking.step = 'idle';
            this._addBotMessage(
                this._tr(
                    'chat_booking_error',
                    '⚠️ Не удалось отправить заявку. Пожалуйста, позвоните нам: **+7 (499) 999-99-99**'
                ),
                [],
                [{
                    text: this._tr('chat_booking_contact_link', 'Контакты клиники →'),
                    url: '/pages/contact.html'
                }]
            );
        }
    };
}
