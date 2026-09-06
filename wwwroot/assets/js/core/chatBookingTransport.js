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
