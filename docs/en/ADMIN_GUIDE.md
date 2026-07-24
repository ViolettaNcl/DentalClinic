[⬅ Back to README](../../README.en.md)

# 👑 Administrator Guide

*[🇷🇺 Русская версия](../ADMIN_GUIDE.md)*

The admin panel is available under the "Admin" tab after logging in with an admin account
(`/api/auth/admin/login`).

📷 *[screenshot placeholder: admin panel, "Requests" tab]*

## 1. Appointment requests

- All requests (including `pending`) are visible under **"Requests"**, with filters by
  status and date.
- **Confirm / cancel** a request with the corresponding buttons; the patient instantly
  receives a notification (SignalR) if they're registered.
- **Book by phone** — a separate form for when a patient called and needs to be entered
  into the system manually (`POST /api/appointmentrequest/admin/phone`).
- Requests that stayed `pending` for longer than `PendingRequestExpiryDays` days (default
  14, configurable in `appsettings.json` → `BackgroundJobs`) are automatically flagged as
  expired by the background cleanup job, if it's enabled (`CleanupEnabled`).

## 2. Doctors

**"Doctors"** section: add, edit, and deactivate doctor cards — full name (including
translations into 4 languages), specialization, years of experience, bio. Inactive
doctors are hidden from the public site, but their appointment history is preserved.

## 3. Services and price list

**"Services"** section: category, name, price range (`from`–`to`), unit, link to the
service page. Changes are reflected immediately:
- on the public "Services" page;
- in the AI assistant's replies (prices are pulled from the database live, no server
  restart needed).

Deleting a service is a "soft" delete: it's flagged inactive (`IsActive=false`) rather
than physically removed, so history in older requests/reports isn't lost.

## 4. Review moderation

**"Reviews"** section with three tabs: **pending / approved / rejected**.
- When rejecting, always provide a reason — the patient will see it in their dashboard.
- Approved reviews appear immediately on the public reviews page.

## 5. Statistics and reports

📷 *[screenshot placeholder: "Statistics" section with charts]*

**"Statistics"** section:
- summary metrics for the selected period (number of requests, reviews, conversion, etc.);
- **"Export to Excel"** button — exports period data as `.xlsx` (`/api/adminstats/export/xlsx`);
- **"Printable report"** button — a print/PDF-ready version via the browser
  (`/api/adminstats/export/report`).

## 6. AI chatbot monitoring

The **"AI Chat"** section shows recent chatbot conversation sessions and usage statistics
over a period — useful for understanding what visitors ask about most, so you can update
the price list/service descriptions accordingly.

## 7. Admin profile

Password and avatar changes are in the "Profile" section, similar to the patient
dashboard, but with the admin role.

## 8. Rate limits and abuse protection

The system automatically throttles action frequency (see `docs/en/SECURITY.md`):
- no more than 3 appointment requests per minute per IP;
- no more than 8 login attempts per minute per IP;
- no more than 15 chatbot messages per minute per IP;
- no more than 40 translation requests per minute per IP.

When a limit is exceeded, the user sees a temporary-block message — this is expected
behavior, not a bug.
