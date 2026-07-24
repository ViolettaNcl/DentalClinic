[⬅ Back to README](../../README.en.md)

# 👤 User Guide (Patient)

*[🇷🇺 Русская версия](../USER_GUIDE.md)*

This guide describes how to use the clinic website — both without registering and inside
the patient dashboard.

📷 *[screenshot placeholder: home page with the request form]*

## 1. Booking an appointment without registering

1. Open the site's home page.
2. Fill in the appointment request form: name, phone, desired date, doctor (optional), comment.
3. Click "Submit request".
4. The request lands in the admin panel with status "pending". You'll be contacted at
   the phone number you provided to confirm it.

> Registration isn't required for your first request — but if you do register, you can
> track its status, reschedule, and cancel it yourself, plus receive realtime
> notifications.

## 2. Registration and login

1. Click "Log in" → "No account? Sign up".
2. Enter your name, email, and password.
3. After registering, log in with the same email and password.

## 3. Patient dashboard

📷 *[screenshot placeholder: patient dashboard, "My appointments" tab]*

Once logged in, the "Dashboard" tab is available, where you can:

- **My appointments** — a list of your requests with statuses:
  - `pending` — awaiting admin confirmation;
  - `confirmed` — confirmed;
  - `cancelled` — cancelled;
  - `completed` — the appointment took place.
- **Reschedule** — a "Reschedule" button on requests that are pending/confirmed.
- **Cancel** — a "Cancel" button.
- **My reviews** — reviews you've submitted and their moderation status (pending /
  published / rejected — with a reason if rejected).
- **Profile** — change your name, phone, avatar, password.

## 4. Notifications

The notification bell in the site header updates **instantly** (no page reload) when:
- an admin confirms or cancels your request;
- your review passes or fails moderation;
- your appointment date is approaching (a reminder 24 hours ahead).

## 5. Reviews

1. Go to the "Reviews" section on the public site or in your dashboard.
2. Give a rating (1–5) and write your review text.
3. The review is sent to the admin for moderation and will appear on the site once approved.
4. Reviews on the site can be switched to your preferred language — translation is done
   automatically via AI and cached, so it doesn't repeat the work every time.

## 6. "Denta" AI assistant

📷 *[screenshot placeholder: the chatbot window open]*

There's a chatbot icon in the bottom-right corner of the site. It can:
- answer questions about service prices and doctor specializations (pulled from the
  current price list, not hardcoded in advance);
- read its answer out loud (a play button);
- reply in the site's current interface language.

## 7. Switching language

There's a language switcher in the site header: 🇷🇺 Russian, 🇬🇧 English, 🇫🇷 French,
🇬🇷 Greek, 🇸🇦 Arabic. Switching happens instantly, without a page reload.

---

If something doesn't work as described, let us know through the site's "Contact" section
or open an Issue in the project's GitHub repository.
