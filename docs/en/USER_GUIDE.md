# User guide

[Documentation](../README.md) · [Live application](https://dental-clinic-vn.vercel.app/) · [Русский](../USER_GUIDE.md)

This guide covers the public site and patient dashboard. The website supports Russian, English, French, Greek, and Arabic; Arabic switches the page to right-to-left layout.

![DentalClinic home page](../screenshots/home.png)

## 1. Explore the clinic

Visitors can browse services and prices, doctor profiles, clinic information, reviews, and detailed treatment pages without an account. Public information is loaded from explicit public API projections; administrator-only fields are not part of those responses.

Prices and website content are informational. Treatment suitability and final cost require an assessment by the clinic.

## 2. Request an appointment as a guest

1. Open the appointment form.
2. Enter a name, valid phone number, desired future date, optional doctor, and comment.
3. Submit the request.
4. The clinic receives it as `pending` and contacts you to confirm details.

A guest request is not automatically attached to an account created later. If you want self-service history and notifications, sign in before creating the request.

Submitting a request is not confirmation. An appointment becomes confirmed only after the clinic assigns/validates the exact doctor/time and changes its status.

## 3. Register and sign in

Registration requires a name, valid email, and password with at least eight characters including uppercase, lowercase, a number, and a special character.

After successful registration/login, the server creates a protected browser session cookie. The password or token is not stored in browser JavaScript storage.

Do not share an account. Sign out on a shared device. A password change requires a new sign-in; invalidation on another server may take up to 45 seconds.

## 4. Patient dashboard

![DentalClinic patient dashboard](../screenshots/patient-dashboard.png)

The dashboard includes:

- **Appointments:** active/history status and permitted actions;
- **Reviews:** submitted reviews and moderation result;
- **Notifications:** durable clinic updates with unread state;
- **Profile:** name, phone, password, and personal avatar.

### Appointment actions

| Status | Meaning | Patient self-service |
|---|---|---|
| `pending` | Waiting for clinic confirmation | May cancel or request another future date |
| `confirmed` | Clinic confirmed doctor/time | Contact the clinic to cancel or reschedule |
| `completed` | Visit completed | No schedule changes |
| `cancelled` | Request cancelled | No patient reactivation; create/contact clinic as appropriate |

These restrictions protect the clinic schedule from uncoordinated changes after confirmation.

## 5. Notifications

The bell can show:

- welcome;
- appointment confirmed/cancelled/completed;
- appointment reminder;
- post-visit follow-up;
- review approved/rejected.

SignalR can deliver updates immediately. If realtime connectivity is unavailable, reloading/restoring the dashboard fetches the durable notification state through REST.

You can mark one/all notifications as read or delete them. Deleting a visible notification does not change the underlying appointment/review state.

## 6. Reviews

1. Sign in as a patient.
2. Submit a rating from 1 to 5 and 10–1,000 characters of text.
3. The review remains `pending` until an administrator decides.
4. Approved reviews become public; rejected reviews show the reason in your dashboard.

Do not include phone numbers, email addresses, private health details, or information about another person. Review translation uses an external AI provider when requested.

## 7. Profile and avatar

Patients can update their display name and phone, change password, and upload a JPG/PNG/WebP avatar up to 3 MB. The service validates the file signature and stores image bytes in SQL.

Only the current authenticated account can read/change its avatar. Removing an avatar does not delete the account.

## 8. Denta assistant

![Denta assistant](../screenshots/chat-bot.png)

Denta can answer bounded questions about published services, prices, doctors, clinic facts, and navigation. It can suggest safe local links, help structure an appointment request, and optionally play a speech version of an answer.

Denta is an informational assistant, not a clinician. Do not rely on it for diagnosis, medication instructions, treatment guarantees, or emergency care. The Smile Meter is a cosmetic illustration, not a prediction of an individual treatment result. For clinical concerns, contact the clinic or an appropriate local emergency service.

External providers may be temporarily unavailable or rate-limited. Some deterministic clinic answers can still work; generated/translation/speech features may degrade gracefully.

## 9. Language and accessibility

![Language selector](../screenshots/language-switcher.png)

Use the header language control to switch among RU/EN/FR/EL/AR. The preference applies without a full page reload where supported. Arabic uses RTL layout.

The project includes keyboard/focus and automated accessibility checks, but if a control is inaccessible, report the page, browser/device, chosen language, and exact step through a GitHub issue without including personal or medical information.

## 10. Privacy and support

The application stores information needed for the requested workflow. Chat IP values are pseudonymized before persistence and cleared after a short configured period; chat messages have a configured retention period.

For account, appointment, or clinical questions, use the clinic contact information shown by the application. For a software bug, open a GitHub issue with sanitized reproduction steps. Security vulnerabilities must follow the private [security policy](../../SECURITY.md), not a public issue.
