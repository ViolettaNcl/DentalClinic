# DentalClinic Audit Progress

## Current hardening pass

- Reviewed patient dashboard translation and password-related frontend flows.
- Reviewed authentication/session invalidation and request protection paths.
- Reviewed frontend dynamic rendering points used by notifications and chat flows.
- Centralized synchronous password/appointment validation feedback across all supported UI languages and removed the unused duplicate frontend password policy.
- Reviewed controller/service authorization boundaries for public doctor/service reads, admin writes, maintenance cron secret checks, appointment ownership, and paid translation origin/rate-limit protections.
- Added fail-closed database integrity constraints for review rating/status, doctor experience bounds, service price/order invariants, chat role/language domains, and the durable patient-notification type domain already enforced by application policy.
- Reviewed the paid AI boundary end-to-end: same-origin checks, payload caps, local/distributed quotas, Gemini key handling, model routing, and request-abort propagation.
- Added a shared pre-write cross-role identity guard so the same normalized email cannot be persisted as both a Patient and an Admin, including concurrent writes across instances.
- Removed the legacy fake clinic phone from confirmed-appointment patient flows: server responses now use the configured public clinic phone when present and otherwise direct patients to published Contacts details; the dashboard fallback no longer invents a number.
- Added a configurable server-side row ceiling for admin XLSX/print exports so large date ranges cannot materialize an unbounded result set in memory; oversized exports are rejected explicitly instead of being silently truncated.
- Separated committed patient registration from the non-critical welcome notification: a notification storage failure is logged and detached instead of converting an already-created account into an HTTP failure, while cancellation and invalid notification types still propagate.
- Continuing security, reliability, and consistency checks across controllers and persistence while deployment work remains intentionally paused.

## Completed checks

- JWT/session invalidation flow reviewed.
- Rate limiting architecture reviewed.
- Chat widget language switching flow reviewed.
- Client-side HTML rendering paths reviewed for user-controlled content handling.
- Validation/appointment feedback synchronization reviewed across Russian, English, French, Greek, and Arabic, with regression coverage.
- Public doctor/service projections and Admin-only write boundaries reviewed.
- Maintenance endpoints verified fail-closed on a missing/invalid cron secret using fixed-time comparison.
- Appointment ownership and serializable scheduling transaction protections reviewed.
- Database constraints and migration safety reviewed for review, doctor, service, chat, and notification domain invariants; migrations abort on inconsistent legacy rows instead of silently changing them.
- Cross-role identity integrity reviewed: Patient/Admin creation paths acquire the same transaction-owned SQL application lock before cross-table uniqueness checks and writes. Admin creation joins its existing AdminAccess transaction instead of nesting one; non-relational tests share an in-process gate. Legacy duplicates abort migration for operator review, and no write triggers are installed.
- Confirmed-appointment contact guidance reviewed: API restrictions consume `Clinic:Phone` through the validated public clinic profile and fall back to the Contacts page rather than exposing placeholder clinic data.
- Durable notification persistence reviewed: eight supported patient-notification types are centralized, unsupported types fail before persistence, and `CK_Notifications_Type` enforces the same closed domain in SQL. The migration fails closed if unknown legacy rows exist and never rewrites them.
- Admin export resource usage reviewed: date ranges remain capped at 366 days and row materialization now reads only one sentinel row beyond `AdminExports:MaxRows` (default 25,000, clamped to 100–100,000) before returning HTTP 422 with instructions to narrow the period.
- Registration commit boundary reviewed: the patient row is committed first; only the post-commit welcome write uses the optional durable-notification path. Persistence failure cannot make the client retry an account that already exists, while request cancellation and notification-domain errors remain strict failures.

## Next checks

- Continue controller/service audit for remaining file-upload and public response surfaces.
- Continue database constraint/index review for remaining auth/persistence race conditions.
- Expand regression coverage for remaining edge cases.
- Keep deployment recovery paused until the operator explicitly resumes it.
