[⬅ Back to README](../../README.en.md)

# 📡 REST API Reference

*[🇷🇺 Русская версия](../API.md)*

Base path: `/api`. Data format: JSON. Endpoints that require authorization are marked 🔒
(`Authorization: Bearer <token>` header); admin-only endpoints are marked 🔒👑.

> In `Development` mode, interactive Swagger docs are available at `/swagger` — you can
> try requests directly from the browser.

## Auth — `/api/auth`

| Method | Path | Access | Description |
|---|---|---|---|
| POST | `/register` | public | Register a patient |
| POST | `/login` | public | Patient login, returns a JWT |
| POST | `/admin/login` | public | Admin login, returns a JWT |
| GET | `/profile` | 🔒 patient | Own profile data |
| GET | `/admin/profile` | 🔒👑 | Admin profile data |
| PUT | `/profile` | 🔒 | Update profile |
| PUT | `/change-password` | 🔒 | Change password |

Login/registration are throttled by the `auth` policy — no more than 8 requests per
minute per IP (brute-force protection).

## Appointment Request — `/api/appointmentrequest`

| Method | Path | Access | Description |
|---|---|---|---|
| POST | `/` | public | Create an appointment request (rate limit: 3/min per IP) |
| GET | `/patient/{patientId}` | 🔒 patient | Requests for a specific patient |
| GET | `/admin/all` | 🔒👑 | All requests (for the admin panel) |
| PUT | `/{id}` | 🔒👑 | Update a request (status, doctor, date, etc.) |
| POST | `/admin/phone` | 🔒👑 | Create a request on behalf of a patient who called by phone |
| PUT | `/{id}/cancel` | 🔒 patient | Cancel own request |
| PUT | `/{id}/reschedule` | 🔒 patient | Reschedule own request |

## Doctor — `/api/doctor`

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/` | public | List of active doctors |
| GET | `/admin/all` | 🔒👑 | All doctors, including inactive |
| POST | `/` | 🔒👑 | Add a doctor |
| PUT | `/{id}` | 🔒👑 | Update doctor data |

## Doctor Schedule — `/api/doctorschedule`

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/` | public | Doctor's schedule/availability for a date |

## Service — `/api/service`

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/` | public | List of active services (price list) |
| GET | `/admin/all` | 🔒👑 | All services, including hidden ones |
| POST | `/` | 🔒👑 | Add a service |
| PUT | `/{id}` | 🔒👑 | Update a service |
| DELETE | `/{id}` | 🔒👑 | Deactivate a service (soft delete) |

## Review — `/api/review`

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/approved` | public | Approved reviews for the public page |
| POST | `/translate` | public | Translate review text into a target language (rate limit: 40/min) |
| GET | `/patient/{patientId}` | 🔒 patient | Own reviews (any status) |
| POST | `/` | 🔒 patient | Submit a review |
| POST | `/{id}/mark-read` | 🔒 patient | Mark a review notification as read |
| GET | `/admin/pending` \| `/admin/approved` \| `/admin/rejected` | 🔒👑 | Reviews by moderation status |
| PUT | `/admin/{id}/moderate` | 🔒👑 | Approve/reject a review |

## Notification — `/api/notification`

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/` | 🔒 | Own notifications |
| GET | `/unread-count` | 🔒 | Unread count |
| PUT | `/{id}/read` \| `/read-all` | 🔒 | Mark as read |
| DELETE | `/{id}` \| `/` | 🔒 | Delete one / all notifications |

**Realtime:** the same events arrive instantly via the SignalR hub
`/hubs/notifications` (see `wwwroot/assets/js/services/realtime.js`) — REST is used for
the initial load and as a fallback if the WebSocket connection is unavailable.

## Chat (AI assistant) — `/api/chat`

| Method | Path | Access | Description |
|---|---|---|---|
| POST | `/` | public | Send a message to the bot, get a full response (rate limit: 15/min) |
| POST | `/stream` | public | Same, but streamed response (server-sent chunks) |
| POST | `/tts` | public | Convert text to speech via ElevenLabs |
| GET | `/admin/sessions` | 🔒👑 | Recent chat sessions |
| GET | `/admin/stats` | 🔒👑 | Chatbot statistics over a period |

The bot's response context (prices, doctors) is assembled on the fly from the database
by `ChatKnowledgeService` with caching — when the price list changes via the admin
panel, the bot immediately knows the current prices.

## Translate — `/api/translate`

| Method | Path | Access | Description |
|---|---|---|---|
| POST | `/` | public | Translate arbitrary UI text |

## Avatar — `/api/avatar`

| Method | Path | Access | Description |
|---|---|---|---|
| POST | `/` | 🔒 | Upload own avatar |
| DELETE | `/` | 🔒 | Delete own avatar |

## Admin Stats — `/api/adminstats`

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/export/xlsx?from=&to=` | 🔒👑 | Export statistics to Excel for a period |
| GET | `/export/report?from=&to=` | 🔒👑 | Printable report for a period |

---

### Error format

Unhandled exceptions are returned in a single format (see `Program.cs`):

```json
{ "message": "Произошла внутренняя ошибка сервера" }
```

Rate limit exceeded (`429 Too Many Requests`):

```json
{ "message": "Слишком много запросов с вашего IP. Попробуйте через минуту." }
```

> Note: these two messages are still returned in Russian by the current backend code —
> translate the exception handler in `Program.cs` if you need English error messages too.
