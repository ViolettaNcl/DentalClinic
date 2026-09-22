# REST API reference

[Documentation](../README.md) · [Project README](../../README.md) · [Русский](../API.md)

The application exposes JSON endpoints under `/api`, a SignalR hub at `/hubs/notifications`, and a database-aware health endpoint at `/health`. Swagger UI is enabled only in the `Development` environment at `/swagger`.

This is a route/access reference, not a generated schema. Request and response contracts are defined by the controller actions and model DTOs in the repository.

## Authentication and request trust

The browser session is a signed JWT transported in the `dc_auth` cookie. Login and registration set the cookie with `HttpOnly`, `SameSite=Strict`, root path, and `Secure` on HTTPS. The token is not returned in the response body and is not readable by frontend JavaScript.

`JwtBearer` validation also understands a conventional `Authorization: Bearer <token>` header when a trusted non-browser integration already has a token, but the built-in login routes intentionally issue only the protected cookie.

Access labels used below:

| Label | Meaning |
|---|---|
| Public | No authenticated session required |
| Patient | JWT role `Patient`; ownership checks apply where an ID is present |
| Admin | JWT role `Admin` |
| Super-admin | Admin session plus a current `Admins.IsSuperAdmin` database check |
| Cron | `Authorization: Bearer <CRON_SECRET>`; excluded from Swagger |

In production, session-creating/mutating requests and unsafe cookie-authenticated API requests must be same-origin. Paid AI routes additionally reject missing/cross-origin `Origin` headers. Development and test environments allow direct tooling requests.

## Auth — `/api/auth`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `POST` | `/register` | Public | Register a patient, create a protected session cookie |
| `POST` | `/login` | Public | Patient login, create a protected session cookie |
| `POST` | `/admin/login` | Public | Administrator login, create a protected session cookie |
| `POST` | `/logout` | Public/session-aware | Expire the session cookie; update the local revocation cache when claims are available |
| `GET` | `/session` | Patient/Admin | Restore non-secret UI session metadata from validated claims |
| `GET` | `/profile` | Patient | Read the current patient's profile |
| `GET` | `/admin/profile` | Admin | Read the current administrator's profile |
| `PUT` | `/profile` | Patient | Update the current patient's name/phone |
| `PUT` | `/change-password` | Patient | Verify and change password, increment token version, end the current session |

Registration and both login routes are limited to 8 attempts per minute per client in production. Patient/admin email identity is unique across roles through serialized application logic and migration checks.

## Administrator access — `/api/admin-access`

All routes require an Admin JWT; each action then verifies that the current account is a super-admin.

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/` | Super-admin | List administrator accounts and access level |
| `POST` | `/` | Super-admin | Create an administrator or super-admin account |
| `PUT` | `/{id}/super-admin` | Super-admin | Promote or demote an administrator |
| `PUT` | `/{id}/password` | Super-admin | Reset an administrator password and revoke older sessions |
| `DELETE` | `/{id}` | Super-admin | Delete another administrator |

The service prevents self-deletion and prevents demotion/deletion of the final super-admin. Mutations are serialized across SQL Server instances with an application lock.

## Appointment requests — `/api/appointmentrequest`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `POST` | `/` | Public/Patient | Create a guest or authenticated appointment request |
| `GET` | `/patient/{patientId}` | Patient/Admin | Read bounded active/history lists; patient ownership is enforced |
| `GET` | `/admin/all` | Admin | Read the bounded CRM appointment list |
| `PUT` | `/{id}` | Admin | Update schedule fields and perform an allowed status transition |
| `POST` | `/admin/phone` | Admin | Create a request received by phone |
| `PUT` | `/{id}/cancel` | Owning patient/Admin | Cancel an own pending request |
| `PUT` | `/{id}/reschedule` | Owning patient/Admin | Reschedule an own pending request |

Public creation is limited to 3 requests per minute per client in production. Scheduling validation applies configured working hours, clinic time zone, lead time, slot interval, appointment duration, active-doctor checks, and overlap detection. Confirmed appointments cannot be cancelled/rescheduled by the patient API; the patient is directed to contact the clinic.

Bounded reads may return `X-Result-Truncated`, `X-Active-Truncated`, or `X-History-Truncated` response headers.

## Doctors — `/api/doctor`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/` | Public | Up to 200 active doctor public projections |
| `GET` | `/admin/all` | Admin | Complete administrator projection including inactive doctors |
| `POST` | `/` | Admin | Create a doctor profile |
| `PUT` | `/{id}` | Admin | Update profile/status; future bookings block unsafe deactivation |
| `POST` | `/{id}/photo` | Admin | Upload JPG/PNG/WebP, max 5 MB, with signature validation |
| `DELETE` | `/{id}/photo` | Admin | Remove the stored doctor photo |
| `GET` | `/{id}/photo` | Public | Return durable doctor image bytes |

When the public result is capped, `X-Result-Truncated: true` is returned.

## Doctor schedule — `/api/doctorschedule`

Both routes are **Admin-only** and accept a maximum 32-day inclusive calendar range.

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/?doctorId=&from=YYYY-MM-DD&to=YYYY-MM-DD` | Admin | Confirmed schedule events (compatibility view) |
| `GET` | `/availability?doctorId=&from=YYYY-MM-DD&to=YYYY-MM-DD` | Admin | Slot availability using the same rules as appointment validation |

## Services — `/api/service`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/` | Public | Up to 500 active service projections |
| `GET` | `/admin/all` | Admin | Complete service catalogue including inactive rows |
| `POST` | `/` | Admin | Create a catalogue item |
| `PUT` | `/{id}` | Admin | Update pricing, content, page slot, ordering, or active state |
| `DELETE` | `/{id}` | Admin | Deactivate a service (soft delete) |

Price ranges and sort order are database-constrained. Active `(PageUrl, SortOrder)` slots are unique when a fixed positive page slot is used.

## Reviews — `/api/review`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/approved` | Public | Bounded recent approved reviews with total count, average, and `truncated` flag; no pagination parameters |
| `POST` | `/translate` | Public | Same-origin AI translation, limited to 40/minute |
| `GET` | `/patient/{patientId}` | Patient/Admin | Reviews owned by the patient |
| `POST` | `/` | Patient | Submit a 1–5 rating and review text for moderation |
| `POST` | `/{id}/mark-read` | Owning patient/Admin | Mark the review decision notification as read |
| `GET` | `/admin/list/{status}?page=&pageSize=` | Admin | Paginated moderation list for an allowed status |
| `GET` | `/admin/summary` | Admin | Counts by moderation status |
| `GET` | `/admin/pending` | Admin | Compatibility list for pending reviews |
| `GET` | `/admin/approved` | Admin | Compatibility list for approved reviews |
| `GET` | `/admin/rejected` | Admin | Compatibility list for rejected reviews |
| `PUT` | `/admin/{id}/moderate` | Admin | Approve/reject with transactional durable notification |

## Notifications — `/api/notification`

All routes are Patient-only and always derive ownership from the authenticated claim.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/` | List the current patient's notifications |
| `GET` | `/unread-count` | Return unread count |
| `PUT` | `/{id}/read` | Mark one owned notification as read |
| `PUT` | `/read-all` | Mark all owned notifications as read |
| `DELETE` | `/{id}` | Delete one owned notification |
| `DELETE` | `/` | Delete all owned notifications |

Realtime delivery uses the authenticated SignalR hub `/hubs/notifications`. The same-origin `dc_auth` cookie is sent during hub negotiation; the JWT is deliberately not placed in a query string.

## Denta assistant — `/api/chat`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `POST` | `/` | Public, same-origin in production | Return a complete structured assistant response |
| `POST` | `/stream` | Public, same-origin in production | SSE transport with validated structured output and final metadata |
| `POST` | `/tts` | Public, same-origin in production | Stream ElevenLabs `audio/mpeg`; 4 requests/minute |
| `GET` | `/admin/sessions` | Admin | Bounded recent conversation sessions |
| `GET` | `/admin/stats` | Admin | Bounded usage statistics |

Chat generation is limited to 15 requests/minute. Production paid-provider quotas are backed by SQL counters in addition to the process-local limiter. `ChatKnowledgeService`, `DentaClinicRouter`, and the administrator-managed knowledge base supply authoritative clinic facts before provider generation.

`/stream` is an SSE endpoint, but the provider's structured JSON is validated before application events are emitted. See the [accepted streaming decision](../DENTA_STREAMING_DECISION.md).

## Translation — `/api/translate`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `POST` | `/` | Public, same-origin in production | Translate bounded UI text via Gemini; 40 requests/minute |

## Avatar — `/api/avatar`

All routes require a Patient or Admin session and operate only on the current account.

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/` | Upload JPG/PNG/WebP, max 3 MB, with extension/signature validation |
| `GET` | `/content` | Return the current account's SQL-backed image; global authenticated API middleware applies `no-store` |
| `DELETE` | `/` | Remove avatar bytes and metadata |

## Public clinic profile — `/api/clinic`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/profile` | Public | Return configured public phone, email, address, hours, and complete coordinate pair |

Missing values remain `null`; the API does not invent production clinic facts.

## Clinic knowledge — `/api/clinic-knowledge`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/` | Admin | List managed Denta knowledge rows |
| `POST` | `/` | Admin | Create a knowledge row |
| `PUT` | `/{id}` | Admin | Update content, keywords, order, or active state |
| `DELETE` | `/{id}` | Admin | Deactivate a row |

Managed content is treated as untrusted data, sanitized, ranked against the query, and bounded before it enters the assistant prompt.

## Admin analytics — `/api/adminstats`

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/summary` | Admin | Lifetime totals, current-month count, last-30-day series, and doctor breakdown; no date parameters |
| `GET` | `/export/xlsx?from=&to=` | Admin | Bounded XLSX export |
| `GET` | `/export/report?from=&to=` | Admin | Bounded printable HTML report |

Invalid or excessive date ranges return `400`; the maximum inclusive export range is 366 days. Exceeding the configured row limit returns `422 Unprocessable Entity` rather than silently truncating.

## Maintenance — `/api/maintenance`

These routes are excluded from Swagger and require `Authorization: Bearer <CRON_SECRET>`.

| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/reminders` | Cron | Create next-day appointment reminders |
| `GET` | `/follow-ups` | Cron | Create one post-visit follow-up per eligible appointment |
| `GET` | `/cleanup` | Cron | Cancel stale pending requests only when cleanup is explicitly enabled |
| `GET` | `/chat-retention` | Cron | Delete expired messages and clear older IP pseudonyms |

## Health — `/health`

`GET /health` is public and returns JSON for the application and EF Core database check. A healthy response uses HTTP `200`; an unhealthy dependency uses the health-check status selected by ASP.NET Core.

## Response and error behavior

Most controller errors use a JSON object with a human-readable `message`. Some operational responses also include a stable `code`, for example the global transient-database response:

```json
{
  "message": "База данных временно отвечает медленно. Повторите запрос через несколько секунд.",
  "code": "database_temporarily_unavailable"
}
```

Common status codes:

| Status | Meaning |
|---|---|
| `400` | Invalid request or forbidden state transition |
| `401` | Missing/invalid session or cron secret |
| `403` | Wrong role, ownership, super-admin check, or origin boundary |
| `404` | Resource not found |
| `409` | Scheduling, identity, or catalogue integrity conflict |
| `413` | Request/file payload exceeds the configured bound |
| `422` | Export request is valid but exceeds safe operational bounds |
| `429` | Local/distributed quota exceeded; production quota responses include `Retry-After: 60` |
| `503` | Transient database connectivity problem |

User-facing backend messages are currently mixed Russian/English; clients should branch on HTTP status and stable codes where available rather than parsing message text.
