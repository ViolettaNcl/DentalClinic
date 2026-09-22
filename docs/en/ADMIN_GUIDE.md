# Administrator guide

[Documentation](../README.md) · [API](API.md) · [Русский](../ADMIN_GUIDE.md)

This guide covers the clinic-facing dashboard and super-admin access workflows. Menu labels can vary by selected UI language; API route names remain stable.

![DentalClinic administrator dashboard](../screenshots/admin-dashboard.png)

## 1. Access model

Administrators sign in through the Admin login flow, which creates the same protected `HttpOnly` session cookie used elsewhere in the application. There are two authorization levels:

- **Admin:** manages clinic operations, doctors, services, reviews, analytics, and Denta knowledge.
- **Super-admin:** has all Admin capabilities plus administrator-account management.

Super-admin is a database flag checked at the time of each access-management request. Promotion/demotion therefore does not depend only on an older JWT claim.

The application does not create a known default administrator. The first super-admin must be provisioned by the deployment owner through a controlled process.

## 2. Appointment operations

The appointment workspace combines active/history requests, filters, patient contact, doctor assignment, and status management.

Core states:

- `pending` — awaiting a clinic decision; it already reserves the chosen doctor/time when both are present;
- `confirmed` — accepted with a valid doctor and exact time;
- `completed` — visit completed; terminal state;
- `cancelled` — cancelled by clinic/patient/optional cleanup; an admin may reactivate it to `pending` after revalidation.

When confirming an appointment, the system requires both an active doctor and exact appointment time. It checks clinic time zone, working hours, lead time, slot interval, appointment duration, and overlap with existing `pending`/`confirmed` bookings.

Patient notification is created after an appointment status change. A failure in a non-critical optional notification does not roll back an already committed scheduling change; the primary operation remains authoritative.

### Phone requests

Use the phone-request form for a caller who is not creating the request through their patient account. A phone/guest request has no patient ownership link, so it is managed by clinic staff rather than the patient dashboard.

### Cleanup

Stale `pending` cancellation is disabled by default. It runs only when the deployment owner explicitly sets `BackgroundJobs:CleanupEnabled=true`. Do not enable it without agreeing on the expiry period and communication policy.

## 3. Doctor calendar

The Admin-only calendar uses `/api/doctorschedule/availability` as its source of truth. It calculates availability with the same service used by appointment writes, so the UI should not show a slot as free when the API would reject it.

The request range is limited to 32 inclusive days. Resolve future appointments before deactivating a doctor; the API blocks deactivation while future `pending` or `confirmed` bookings exist.

## 4. Doctor profiles

Administrators can manage:

- primary and EN/FR/EL/AR names;
- specialization, experience, biography, role title, education, skills, philosophy, and optional profile metrics;
- active/inactive visibility;
- JPG, PNG, or WebP photo up to 5 MB.

Photos are signature-validated and stored durably in SQL. Never upload patient images or media without documented authorization and an appropriate privacy basis.

## 5. Services and pricing

Service rows drive the public catalogue and are part of Denta's authoritative clinic context. Administrators can change category, name, description, price range, unit, retrieval keywords, page URL, order, and visibility.

Rules enforced by the application/database:

- prices cannot be negative;
- `PriceTo`, when present, cannot be lower than `PriceFrom`;
- sort order cannot be negative;
- two active rows cannot claim the same positive `(PageUrl, SortOrder)` slot;
- delete is a soft deactivation, preserving historical context.

Treat prices as production facts. Verify public pages and Denta after a catalogue change.

## 6. Denta knowledge base

The knowledge workspace manages clinic facts that do not naturally belong to services, doctor profiles, or the public clinic profile.

Each row has category, title, content, optional keywords, order, and active state. Good entries are:

- concise and factual;
- approved by the clinic owner;
- free of secrets, patient data, diagnosis, and ambiguous promises;
- written so they remain correct when quoted independently.

The backend sanitizes and bounds active rows, ranks them against the user's question, and treats them as untrusted data below the medical-safety policy. Deleting a row deactivates it.

## 7. Review moderation

The dashboard provides paginated `pending`, `approved`, and `rejected` views plus summary counts.

- Approve only content appropriate for public display.
- Rejection requires a reason visible to the patient.
- The moderation decision and durable notification commit in one database transaction.
- SignalR delivery occurs after commit and is best effort; the notification remains available through REST even after a reconnect.

Do not publish personal health details, contact information, or content that the reviewer was not authorized to disclose.

## 8. Analytics and exports

![DentalClinic administrator analytics](../screenshots/admin-stats.png)

The summary endpoint uses fixed periods: lifetime totals, the current month, and a 30-day series. It does not accept a custom date range. XLSX and printable HTML exports accept `from`/`to`; a range exceeding 366 inclusive days returns `400`, while exceeding the configured row cap (25,000 by default) returns `422`.

Exports can contain operational/patient-linked information. Store and share them according to the clinic's access, retention, and deletion policy.

## 9. Denta monitoring

Recent sessions and statistics are bounded administrative views. Logs may contain user-entered text; use them to improve clinic content, not to infer a diagnosis or expose user questions outside authorized operations.

Raw IP addresses are not stored. A pseudonym is retained briefly for abuse protection, and chat messages are deleted according to configured retention.

## 10. Administrator access management

Super-admins can list accounts, create admins, grant/revoke super-admin status, reset another admin's password, and delete another account.

Safeguards:

- cannot delete the current active account;
- cannot delete or demote the final super-admin;
- email cannot duplicate another admin or patient;
- password reset/access changes increment the persisted token version; another process may retain an older version in its cache for up to 45 seconds;
- concurrent SQL mutations are serialized.

Grant super-admin only to people who need account-management authority. Use unique accounts rather than shared credentials so access can be revoked individually.

## 11. Operational troubleshooting

| Symptom | First checks |
|---|---|
| A valid slot is rejected | Clinic time zone/hours, lead time, duration overlap, doctor active state |
| Doctor cannot be deactivated | Resolve future `pending`/`confirmed` appointments first |
| Denta gives no generated reply | Gemini configuration/quota; verify deterministic clinic facts still work |
| Realtime bell is delayed | Check network/CDN/SignalR; reload REST notification state |
| Export returns `400` / `422` | Check dates and the 366-day range / reduce rows below the configured cap |
| Super-admin action returns `403` | Confirm the current account's `IsSuperAdmin` state in the database |
| Login stops working after password/access change | Expected session revocation; sign in again |

For incidents, secrets, migrations, and releases, use the [deployment](DEPLOYMENT.md) and [security](SECURITY.md) guides rather than bypassing a safeguard in the dashboard.
