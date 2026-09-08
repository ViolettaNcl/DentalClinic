import { readFile, writeFile } from 'node:fs/promises';

const localeCopies = {
  ru: {
    patient_confirmed_call_required: 'Подтверждённую запись можно изменить только через администратора клиники. Актуальные контакты доступны на странице «Контакты».',
    chat_booking_error: '⚠️ Не удалось отправить заявку. Попробуйте ещё раз позже или откройте страницу контактов клиники.'
  },
  en: {
    patient_confirmed_call_required: 'A confirmed appointment can only be changed through clinic staff. Current contact details are available on the Contacts page.',
    chat_booking_error: "⚠️ Couldn't send the request. Please try again later or open the clinic Contacts page."
  },
  fr: {
    patient_confirmed_call_required: "Un rendez-vous confirmé ne peut être modifié que par l'équipe de la clinique. Les coordonnées à jour sont disponibles sur la page Contacts.",
    chat_booking_error: "⚠️ Impossible d'envoyer la demande. Réessayez plus tard ou ouvrez la page Contacts de la clinique."
  },
  el: {
    patient_confirmed_call_required: 'Ένα επιβεβαιωμένο ραντεβού μπορεί να αλλάξει μόνο μέσω του προσωπικού της κλινικής. Τα ενημερωμένα στοιχεία επικοινωνίας βρίσκονται στη σελίδα Επικοινωνία.',
    chat_booking_error: '⚠️ Δεν ήταν δυνατή η αποστολή του αιτήματος. Δοκιμάστε ξανά αργότερα ή ανοίξτε τη σελίδα Επικοινωνία της κλινικής.'
  },
  ar: {
    patient_confirmed_call_required: 'لا يمكن تغيير الموعد المؤكد إلا من خلال فريق العيادة. تتوفر بيانات التواصل المحدثة في صفحة الاتصال.',
    chat_booking_error: '⚠️ تعذر إرسال الطلب. حاول مرة أخرى لاحقًا أو افتح صفحة الاتصال الخاصة بالعيادة.'
  }
};

const obsoleteClinicFactKeys = [
  'contact_card_address_text',
  'contact_hours_weekdays',
  'contact_hours_sunday'
];

for (const [locale, replacements] of Object.entries(localeCopies)) {
  const path = `wwwroot/assets/i18n/${locale}.json`;
  const raw = await readFile(path, 'utf8');
  const dictionary = JSON.parse(raw.replace(/^\uFEFF/, ''));

  dictionary.patient_confirmed_call_required = replacements.patient_confirmed_call_required;
  dictionary.chat_booking_error = replacements.chat_booking_error;
  for (const key of obsoleteClinicFactKeys) delete dictionary[key];

  await writeFile(path, `${JSON.stringify(dictionary, null, 2)}\n`, 'utf8');
}

const chatBotPath = 'wwwroot/assets/js/core/chatBot.js';
const legacyChatFallback = '⚠️ Не удалось отправить заявку. Пожалуйста, позвоните нам: **+7 (499) 999-99-99**';
const safeChatFallback = '⚠️ Не удалось отправить заявку. Попробуйте ещё раз позже или откройте страницу контактов клиники.';
let chatBot = await readFile(chatBotPath, 'utf8');
const occurrences = chatBot.split(legacyChatFallback).length - 1;
if (occurrences !== 1) {
  throw new Error(`Expected exactly one retired chat booking fallback, found ${occurrences}.`);
}
chatBot = chatBot.replace(legacyChatFallback, safeChatFallback);
await writeFile(chatBotPath, chatBot, 'utf8');

console.log('Retired clinic phone/address/hour fallbacks removed from runtime assets.');
