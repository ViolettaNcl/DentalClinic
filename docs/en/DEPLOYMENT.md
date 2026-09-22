# Deployment guide

[Documentation](../README.md) · [Project README](../../README.md) · [Русский](../DEPLOYMENT.md)

The primary public topology is an ASP.NET Core 10 container service on Vercel backed by an external SQL Server. `Dockerfile.vercel` builds the service and `vercel.json` routes all traffic to service `app`.

Target domain: [https://dental-clinic-vn.vercel.app](https://dental-clinic-vn.vercel.app/)

## 1. Current release policy

`vercel.json` currently contains:

```json
{
  "git": {
    "deploymentEnabled": false
  }
}
```

This pauses Vercel Git deployments. The regression test `tests/js/vercel-deploy-policy.test.js` locks that state. It does **not** pause GitHub Actions: a push to `main`, including documentation-only changes, runs CI and can trigger GHCR publication, configured FTPS deployment, and VCR retention. A documentation branch/PR lets reviewers inspect the change before those main-branch workflows run.

A production release must therefore be initiated by an authorized maintainer from the Vercel dashboard or an already linked/authenticated Vercel CLI environment. Do not tell contributors that merging `main` automatically deploys while this flag remains false.

To resume Git-triggered deployments in the future, make it an explicit operational change: update `vercel.json`, update the deployment-policy test, validate Preview, confirm branch/environment settings in Vercel, and document the approval/rollback process in the same pull request.

## 2. Required production configuration

Configure secrets and environment-specific values in Vercel Project Settings, not in `vercel.json`, the Dockerfile, source, screenshots, or GitHub issues.

| Variable | Purpose | Requirement |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | External SQL Server/Azure SQL connection | Required |
| `Jwt__Key` | Random HMAC signing key, at least 32 UTF-8 bytes | Required |
| `Jwt__Issuer` | Expected token issuer | Required |
| `Jwt__Audience` | Expected token audience | Required |
| `Jwt__ExpiryMinutes` | Session lifetime; application clamps to 5–1440 minutes | Optional, default 120 |
| `CRON_SECRET` | Bearer secret for maintenance endpoints | Required on Vercel |
| `AllowedOrigins__0` | Trusted frontend origin when needed | Required for cross-origin frontend; same-origin app is typical |
| `Clinic__Phone`, `Email`, `Address`, `Hours` | Public clinic profile | Required for production facts |
| `Clinic__Latitude`, `Longitude` | Optional complete coordinate pair | Optional |
| `Scheduling__TimeZoneId` | Clinic-local time zone | Recommended; default `Europe/Moscow` |
| `Scheduling__AppointmentDurationMinutes` | Collision window | Optional, default 60 |
| `Scheduling__SlotIntervalMinutes` | Valid start-time interval | Optional, default 30 |
| `Scheduling__MinimumLeadMinutes` | Minimum future lead | Optional, default 30 |
| `Gemini__ApiKey` | Denta and translation provider | Required for AI features |
| `ElevenLabs__ApiKey`, `VoiceId` | Text-to-speech | Optional |
| `Chat__MessageRetentionDays` | Chat row retention, clamped 1–365 | Optional, default 30 |
| `Chat__IpRetentionHours` | IP-pseudonym retention, clamped 1–168 | Optional, default 24 |
| `ChatKnowledge__MaxItems` | Managed knowledge prompt limit, hard-capped by code | Optional, default 12 |
| `BackgroundJobs__CleanupEnabled` | Enables destructive stale-pending cancellation | Optional, default false |
| `AdminExports__MaxRows` | Bounded export materialization | Optional, default 25,000 |
| `Database__CommandTimeoutSeconds` | SQL command bound | Optional, clamped 5–60 |
| `Database__ConnectTimeoutSeconds` | SQL connect bound | Optional, clamped 5–30 |
| `Database__MaxPoolSize` | SQL pool bound | Optional, clamped 5–50 |

Use a secret manager/password generator. Rotate a credential immediately if it appears in git history, workflow output, an issue, screenshot, or chat; removing the visible text is not sufficient.

## 3. Database and migrations

The Vercel container does not include SQL Server. Use an external managed SQL Server reachable from the deployment.

On relational startup, `Program.cs` calls `Database.MigrateAsync()` before `DbSeeder` and before request serving. `DbSeeder` is protected by a SQL Server application lock so concurrent cold starts do not duplicate starter data.

Before a release containing migrations:

1. Create and verify a restorable database backup.
2. Review every `Up` operation and the data assumptions it enforces.
3. Confirm the application build is compatible with both the pre-migration and post-migration state, or schedule a maintenance window.
4. Release in a controlled manner and monitor startup logs.
5. Verify `/health` and the expected row in `__EFMigrationsHistory`.
6. Test the changed workflow against non-production data before production use.

Application rollback does not automatically roll back the database. Do not run destructive `Down` migrations as a routine rollback; first verify schema/data compatibility and restore from backup when necessary.

The seed process creates catalogue/doctor/knowledge starter rows only when appropriate. It never creates a known default admin. First-administrator provisioning is an operator-controlled action.

## 4. Controlled Vercel release

### Dashboard

1. Confirm the Vercel project is linked to `ViolettaNcl/DentalClinic`, root directory `.`, and the intended production branch/commit.
2. Review Production environment variables and the database backup.
3. Trigger a deployment for the exact approved commit from the Vercel dashboard.
4. Wait for the container to become ready; inspect build and runtime logs for migration/startup failures.
5. Complete the verification checklist below before declaring the release complete.

### CLI from an already linked environment

```bash
npx vercel deploy
npx vercel deploy --prod
```

Use the first command for a Preview verification, then promote/deploy the same reviewed code to Production. Authentication, project linking, scope/team selection, and production authorization are environment-specific and must be managed by the maintainer; never paste Vercel tokens into terminal transcripts or documentation.

`Dockerfile.vercel` uses the platform's `PORT` variable with an 8080 fallback. When `VERCEL=1`, `Program.cs` accepts one forwarded-header hop and clears the known-proxy/network lists; the deployment must restrict direct access to the application so that the header source can be trusted.

## 5. Scheduled maintenance

Vercel containers may suspend, so hosted background services are disabled when `VERCEL=1`. `vercel.json` defines UTC cron schedules that call protected routes:

| UTC | Endpoint | Purpose |
|---|---|---|
| `06:00` daily | `/api/maintenance/reminders` | Next-day reminders |
| `06:10` daily | `/api/maintenance/follow-ups` | Post-visit review prompts |
| `06:15` daily | `/api/maintenance/cleanup` | Stale pending cancellation, only when explicitly enabled |
| `06:30` daily | `/api/maintenance/chat-retention` | Clear old IP pseudonyms and delete expired chat rows |

Vercel sends `Authorization: Bearer <CRON_SECRET>`. The controller fails closed when the secret is missing and compares the supplied value in fixed time. Keep the configured schedule compatible with the deployment plan and account tier.

No dedicated chat-retention hosted worker is registered. Outside Vercel, schedule the protected chat-retention endpoint yourself; keeping a process running is not enough to enforce retention.

Reminder/follow-up delivery uses durable flags and unique idempotency keys so overlapping runs do not create duplicate patient notifications.

## 6. GitHub Actions and delivery artifacts

| Workflow | Role |
|---|---|
| `ci.yml` | Restore/build, Vercel container build, migration discovery, .NET tests, JS tests |
| `codeql.yml` | C# and JavaScript/TypeScript static analysis on push/PR and weekly |
| `cd.yml` | Post-CI test gate, optional FTPS fallback publish, GHCR image push |
| `e2e.yml` | Scheduled/on-push Playwright checks against production |
| `production-smoke.yml` | Hourly health, canonical domain, routes, headers, public API, HTTP→HTTPS |
| `denta-live-smoke.yml` | Optional weekly live Gemini contract test when its secret is configured |
| `vercel-vcr-retention.yml` | Safe retention of Vercel container-registry images |

The FTPS path requires `FTP_SERVER`, `FTP_USERNAME`, `FTP_PASSWORD`, and `FTP_SERVER_DIR`. When any is missing, the job is deliberately skipped. It is a fallback path, not evidence that Vercel deployed.

GHCR publishes `latest` and commit-SHA image tags after the test gate. Vercel registry cleanup requires a scoped `VERCEL_TOKEN` plus project/team/repository settings; it fails closed when it cannot identify protected production images.

## 7. Docker deployment

For a local or controlled single-host environment:

```bash
cp .env.example .env
# Replace all placeholders with environment-appropriate values.
docker compose up --build -d
docker compose ps
docker compose logs -f app
```

Compose exposes the app on port 8080 and SQL Server on 1433. Named volumes retain SQL data and the legacy upload path. Current avatars and doctor photos are stored in SQL and do not depend on the container filesystem.

For a real deployment, do not expose SQL Server publicly unless the network/authentication design explicitly requires it. Configure backups, encryption, restricted ingress, certificate trust, and credential rotation outside Compose.

## 8. Media persistence

New patient/admin avatars and doctor photos are stored as bounded bytes plus MIME metadata in SQL. Browser-facing URLs are cache-busted endpoints. This survives Vercel/container replacement and avoids treating an ephemeral filesystem as durable storage.

Legacy local avatar paths are deleted best-effort after a successful durable update. At materially larger media volume, migrate the byte storage to private/object storage without weakening endpoint authorization or content-signature validation.

## 9. Release verification

### Infrastructure

- [ ] Approved commit/build is the deployed one.
- [ ] Deployment is READY and runtime logs show no migration/startup failure.
- [ ] `GET /health` returns HTTP 200 and reports the `db` check as Healthy.
- [ ] Latest expected migration exists in `__EFMigrationsHistory`.
- [ ] Database backup and restore procedure were verified for this release.
- [ ] No environment secret appears in build/runtime logs.

### Security and identity

- [ ] HTTPS is canonical; HTTP redirects to HTTPS.
- [ ] Expected security headers are present on HTML responses.
- [ ] Patient registration/login/logout/session bootstrap work with an HttpOnly cookie.
- [ ] Admin login and super-admin-only access management enforce role/current DB state.
- [ ] State-changing cross-origin requests and direct production AI calls without valid origin are rejected.
- [ ] Cron routes return 401 without the correct `CRON_SECRET`.

### Product workflows

- [ ] Public services/doctors/clinic profile load without exposing internal fields.
- [ ] Appointment creation rejects past/invalid/conflicting slots.
- [ ] Admin confirmation and patient notifications work.
- [ ] Review moderation persists the decision and notification atomically.
- [ ] Avatar and doctor photo retrieval survive a fresh deployment.
- [ ] Denta returns bounded safe links and degrades safely when a provider is unavailable.
- [ ] RU/EN/FR/EL/AR switch correctly; Arabic layout is RTL.
- [ ] Production smoke and Playwright workflows pass or have an investigated, documented exception.

## 10. Rollback and incident notes

1. Stop further releases and preserve logs/health evidence without copying secrets or patient content.
2. Determine whether the incident is application-only, schema/data, provider, or infrastructure.
3. If schema-compatible, redeploy the last known-good commit/image.
4. If data/schema is affected, follow the verified restore/migration plan instead of guessing with `Down`.
5. Rotate any credential that may have been exposed and invalidate affected sessions/keys.
6. Run the verification checklist after mitigation and record the root cause/action items privately before publishing a sanitized summary.

For security-specific handling, follow the root [SECURITY.md](../../SECURITY.md).
