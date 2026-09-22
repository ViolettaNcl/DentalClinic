# Security architecture and operations

[Documentation](../README.md) · [Security policy](../../SECURITY.md) · [Русский](../SECURITY.md)

This document describes controls implemented in the current repository, the trust boundaries they protect, and residual risks that deployment owners must manage. It is not a certification, penetration-test report, or guarantee that the application is vulnerability-free.

## 1. Assets and trust boundaries

High-value assets include:

- patient/admin identity records and password hashes;
- appointment, review, notification, and chat content;
- administrator privileges and clinic-managed knowledge;
- SQL connection credentials, JWT signing key, cron secret, provider API keys;
- paid-provider quota and production availability.

Inputs treated as untrusted include all browser/API payloads, IDs in routes, uploaded files, origin/forwarding headers, administrator-authored knowledge, AI provider output, CDN-loaded scripts, and database rows created by earlier versions.

## 2. Secrets management

Configuration examples are intended to contain placeholders only; this review is not a complete repository-history secret scan. Real values belong in:

- ASP.NET Core Secret Manager for local development;
- Vercel/GitHub environment secrets for hosted workflows;
- a dedicated secret manager for other production environments.

Ignored files include `appsettings.json`, environment-specific appsettings files, `.env*` (except `.env.example`), certificates, logs, build output, and user uploads.

Operational rules:

1. Never place a real credential in source, a PR, issue, screenshot, test fixture, README command, or chat transcript.
2. Use separate development, staging, and production credentials with least privilege.
3. Rotate immediately after suspected disclosure; history removal alone does not revoke access.
4. Treat production database backups and exported reports as sensitive data.
5. Redact provider/database response bodies before logging or sharing diagnostics.

## 3. Authentication and sessions

| Control | Implementation |
|---|---|
| Password storage | BCrypt hashes; plaintext passwords are never persisted |
| Password policy | Minimum 8 characters with uppercase, lowercase, digit, and special character |
| JWT validation | HMAC signature, issuer, audience, lifetime, zero clock skew |
| Browser transport | `dc_auth` cookie with `HttpOnly`, `SameSite=Strict`, root path, `Secure` on HTTPS |
| JavaScript boundary | Token is not returned in login JSON, local/session storage, or query strings |
| Revocation | `TokenVersion` claim checked against cached/database account state; password/access changes increment it |
| Authorization | Endpoint roles plus resource ownership; super-admin actions re-check `IsSuperAdmin` in SQL |
| Admin safety | Last-super-admin, self-delete, duplicate/cross-role email guards |

The SignalR hub uses the same cookie and does not accept an `access_token` query parameter. Authenticated API responses receive `Cache-Control: no-store` and `Pragma: no-cache`.

Token-version validation uses a 45-second process-local cache. Password/access changes persist a version increment, but other instances may observe it only after cache expiry. Logout deletes the browser cookie and updates the current process cache without persisting an increment; it is not durable global revocation. In Development, token validation tolerates selected transient database failures; production rejects those requests.

The app has no seeded/default administrator credentials. First-admin provisioning is intentionally an operator-controlled process.

## 4. CSRF, origin, and CORS controls

Cookie authentication requires an explicit CSRF strategy. DentalClinic combines:

- `SameSite=Strict` on the session cookie;
- an unsafe-method middleware that requires a matching `Origin` or `Referer` for authenticated/state-changing session routes in production;
- immediate rejection of `Sec-Fetch-Site: cross-site` as a supporting signal;
- stricter same-origin enforcement for paid AI routes, where missing `Origin` is rejected in production;
- a configured CORS allow-list with credentials.

CORS does not authorize a request and is not the primary CSRF control. Direct requests without browser origin evidence are allowed only in Development/Testing. Proxies must preserve the real scheme/host; Vercel forwarded headers are enabled only when `VERCEL=1` with a one-hop limit. The code clears the known proxy/network lists; deployment networking must prevent untrusted clients from reaching the application with forged forwarding headers. The flag alone does not establish proxy trust.

## 5. Abuse and cost controls

| Route family | Limit per client/minute |
|---|---:|
| Appointment creation | 3 |
| Registration/login | 8 |
| Denta chat/stream | 15 |
| ElevenLabs TTS | 4 |
| Translation/review translation | 40 |

Production uses two layers:

- ASP.NET Core fixed-window policies protect each process;
- `DistributedRequestQuotaService` stores pseudonymous fixed-window counters in SQL so limits remain effective across multiple instances.

Paid AI routes also have request-body limits, same-origin checks, cancellation propagation, provider timeouts/fallbacks, and a server-side API-key handler that removes legacy query keys and sends Gemini credentials through the protected header path.

Rate limits reduce abuse; they are not DDoS protection. Edge/network protections and monitoring remain hosting responsibilities.

## 6. Input, data, and concurrency integrity

- Data Annotations bound public DTO length/range/format.
- EF Core database checks enforce closed status/type/language domains and numeric invariants.
- Unique/filtered indexes protect account email, notification idempotency, and active service page slots.
- Appointment scheduling centralizes clinic-local time, working hours, lead time, duration, slot alignment, active doctor, and overlap checks.
- Serializable transactions protect schedule mutations; SQL application locks serialize administrator access, cross-role identity, and seeding where needed.
- Public catalogue endpoints return explicit projections with hard row caps and truncation headers.
- Admin exports reject excessive date ranges with `400` and row-limit overflow with `422`, rather than materializing unbounded results.
- Transient database failures map to a sanitized `503`; unhandled errors do not expose stack traces.

## 7. Upload and media controls

The only upload surfaces are authenticated avatars and Admin-only doctor photos.

- avatar maximum: 3 MB plus a small request overhead;
- doctor photo maximum: 5 MB plus overhead;
- allowed extensions: JPG/JPEG, PNG, WebP;
- content signature is checked against the extension;
- bytes/MIME metadata are stored in SQL, not trusted to an ephemeral server filesystem;
- avatar reads are owner-authenticated; doctor photos are public by product design;
- legacy local avatar deletion resolves only a safe filename under the expected directory and is best effort after durable commit.

This validation checks file type signatures, not image decoding, malware scanning, metadata stripping, or decompression-bomb behavior. Add a dedicated image-processing/scanning boundary if the upload threat/scale increases.

## 8. AI safety and privacy

- Authoritative clinic facts are resolved from configuration/SQL and explicit public projections before free-form generation.
- Administrator-managed knowledge is sanitized, query-ranked, and bounded; it is marked as untrusted prompt data.
- Gemini output uses a structured schema and is validated before application fields reach the browser.
- Suggested links are bounded/localized and filtered through a safe local-link policy.
- Safety filters are designed to reject diagnosis, medication/dosage advice, outcome/pain guarantees, and unsafe emergency guidance; they are not a guarantee of clinical correctness.
- Streaming preserves an SSE contract but does not expose partial structured JSON before validation.
- Provider keys remain server-side and are never returned to JavaScript.

Denta is an informational assistant, not a clinician. UI copy and provider prompts must not present it as diagnosis or emergency care.

Chat privacy:

- raw client IP addresses are converted to SHA-256 pseudonyms before persistence;
- pseudonyms are cleared after a configurable short window (default 24 hours);
- message logs are deleted after a configurable retention period (default 30 days);
- session IDs are bounded/normalized or hashed.

Retention is applied when the protected chat-retention maintenance endpoint runs. Vercel schedules it; other hosting environments need an external scheduler because no chat-retention hosted worker is registered.

Hashing an IP is pseudonymization, not anonymization. Database access and retention still require privacy governance.

## 9. Browser response hardening

The middleware currently sets:

- `X-Content-Type-Options: nosniff`;
- `X-Frame-Options: SAMEORIGIN`;
- `Referrer-Policy: strict-origin-when-cross-origin`;
- `X-Permitted-Cross-Domain-Policies: none`;
- `Permissions-Policy: camera=(), geolocation=(self), payment=(), usb=()`;
- no-store headers for authenticated API responses.

HSTS and HTTPS redirection are enabled outside Development. A Content Security Policy is not currently set because the frontend uses external fonts/maps/CDN/provider integrations that require a tested policy design; adding CSP is a roadmap hardening item, not a control to claim today.

## 10. Maintenance endpoints

`/api/maintenance/*` is hidden from Swagger and requires `Authorization: Bearer <CRON_SECRET>`. The controller fails closed on a missing secret and uses `CryptographicOperations.FixedTimeEquals` for comparison.

Automatic stale-request cancellation remains disabled unless `BackgroundJobs:CleanupEnabled=true`. Reminder/follow-up notifications use database-backed idempotency so concurrent hosted/cron execution does not duplicate messages.

## 11. Supply chain and verification

- Dependabot tracks NuGet, GitHub Actions, and Docker dependencies.
- CodeQL analyzes C# and JavaScript/TypeScript on pushes, pull requests, and a weekly schedule.
- CI builds the app/container, verifies EF migration discovery, and runs .NET/JS tests.
- Production smoke and Playwright workflows verify availability, headers, pages, localization, responsive behavior, and selected accessibility checks.
- A separate live Gemini smoke runs only when its secret is configured.

Pinned versions and automated analysis reduce risk but do not replace review, patching, provider advisories, penetration testing, or least-privilege configuration.

## 12. Known residual risks and next controls

| Risk / limitation | Recommended next step |
|---|---|
| No administrator MFA or password-recovery workflow | Add verified recovery and phishing-resistant MFA before broader admin delegation |
| Symmetric JWT signing key shared by all app instances | Establish rotation runbook; evaluate asymmetric signing/managed identity at larger scale |
| No Content Security Policy | Inventory all external resources, remove unnecessary inline/external dependencies, deploy CSP in report-only mode first |
| CDN-loaded SignalR browser client | Pin/integrity-check or self-host the reviewed client asset |
| Uploads are signature-checked but not decoded/scanned | Add image re-encoding/metadata stripping and malware/resource-exhaustion scanning if risk warrants |
| SQL contains sensitive operational/user data | Enforce provider encryption, least privilege, backup protection, retention, audit, and jurisdictional policy |
| Some user-facing backend messages remain mixed-language | Add stable machine-readable error codes and localize at the client boundary |
| InMemory integration tests cannot prove SQL locking/constraints | Add SQL Server integration coverage for migration/concurrency-critical paths |
| Branch protection is a repository setting, not enforced by code | Enable repository rulesets/required checks in GitHub administration |

## 13. Reporting vulnerabilities

Do not disclose a vulnerability in a public issue. Follow the private process in the root [SECURITY.md](../../SECURITY.md). Revoke exposed secrets immediately and avoid testing against real patient data or disrupting the live service.
