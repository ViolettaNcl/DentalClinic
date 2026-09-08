# DentalClinic Audit Progress

## Phase 0/1 source-hardening checkpoint — COMPLETE

The GitHub source hardening scope for Phase 0/1 has reached its stop condition. Deployment/Vercel recovery remains intentionally paused by operator instruction and is not included in this checkpoint.

The completed pass covers authentication/session safety, authorization boundaries, public response projections, appointment scheduling/ownership, patient/admin identity integrity, durable notifications, review moderation, service-catalogue persistence, file uploads, maintenance endpoints, admin exports, AI/translation paid-API boundaries, regression coverage, migrations, and CI/security gates.

## Completed checks and hardening

- JWT/session invalidation and token-version enforcement reviewed.
- Authentication rate limiting and request-abort propagation reviewed.
- Patient/Admin normalized-email integrity is serialized across instances with the shared identity guard; legacy cross-role duplicates fail migration for operator review rather than being silently changed.
- Public doctor/service/review/clinic responses expose bounded or explicit public projections; admin mutation routes remain role-protected.
- The clinic public profile is configuration-backed. Retired fake clinic phone/address/hours were removed from shipped runtime fallbacks and locale dictionaries; missing contact facts direct users to current Contacts information instead of inventing data.
- Appointment ownership, allowed status transitions, schedule validation, doctor/time requirements, and serializable scheduling transaction protections were reviewed and regression-tested.
- Confirmed-appointment patient contact guidance uses the configured public clinic phone when present and otherwise falls back to the Contacts page.
- Admin appointment status changes remain successful once their database transaction commits even if a non-critical patient notification later fails to persist; cancellation remains strict.
- Registration no longer returns a false failure after the Patient row has committed if the optional welcome notification cannot be persisted; cancellation and invalid notification types remain strict.
- Durable patient notification types are centralized to the verified eight-value domain and enforced by `CK_Notifications_Type`; unknown legacy rows abort the migration without rewrite.
- Review moderation keeps the moderation decision and durable patient notification in the same database transaction, then emits best-effort SignalR delivery only after successful commit.
- Notification reminder/follow-up idempotency uses the durable key/index path so cross-instance maintenance races cannot create duplicate patient notifications.
- Service price/order domain constraints are database-backed. Active service catalogue slots now also have a fail-closed filtered unique index on `(PageUrl, SortOrder)`; concurrent admin writes return controlled HTTP 409 only when the exact slot conflict is confirmed, while unrelated database failures still propagate.
- The only `IFormFile` upload surface is the authenticated avatar endpoint. It enforces a 3 MB request/file limit, extension allow-list, content-signature validation, ownership/role checks, durable database storage, and path-safe best-effort cleanup for legacy local files.
- Maintenance endpoints fail closed when the cron secret is missing/invalid and use fixed-time comparison.
- Admin XLSX/print exports cap date span and row materialization; oversized exports return HTTP 422 instead of silently truncating or loading an unbounded year into memory.
- Chat/translation paid-API boundaries were reviewed for same-origin enforcement, payload limits, distributed/local quotas, provider key handling, model fallback, cancellation, and bounded analytics/history reads.
- Chat role/language, review rating/status, doctor experience, service price/order, appointment status, paid-API usage, notification type, and related persistence invariants have database-level checks where appropriate.
- Client-side dynamic rendering and translation/validation flows were reviewed across Russian, English, French, Greek, and Arabic, with regression coverage for the hardened paths.
- No remaining Phase 0/1 TODO/FIXME marker was found in the targeted repository scan.

## Final verification gates

- PRs #153–#157 were merged only after exact-head CI and CodeQL succeeded.
- The code checkpoint `main` commit `37af341553b55cb47988cc00ef992cc92b46278b` passed its own push-to-main CI: application build, container build, EF migration discovery, .NET/API tests, and JavaScript tests.
- The same code checkpoint passed push-to-main CodeQL for both C# and JavaScript/TypeScript.
- No application PR remained open before this documentation checkpoint was created.

## Repository-governance note

GitHub currently reports no repository rulesets and `main` as `protected: false`. The connected GitHub App cannot administer branch protection (the protection endpoint returns 403), so this repository setting cannot be changed from the current integration. CI and CodeQL are configured to run on both pull requests to `main` and pushes to `main`, and this hardening pass enforced exact-head checks manually before every merge. Enabling branch protection/ruleset enforcement remains an optional repository-administration follow-up, not an unresolved source-code defect.

## Next phase

Phase 0/1 source hardening is complete. The next product work is Phase 2 (AI assistant/knowledge-base improvements, patient automation/reminders, analytics, and branding/domain work). Deployment recovery remains paused until the operator explicitly resumes it.
