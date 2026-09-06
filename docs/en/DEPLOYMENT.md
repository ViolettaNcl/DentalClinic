[⬅ Back to README](../../README.en.md)

# 🚀 Deploying to Vercel

*[🇷🇺 Русская версия](../DEPLOYMENT.md)*

The project runs on Vercel as an ASP.NET Core 10 container service. Its deployment
configuration lives in `Dockerfile.vercel` and `vercel.json`. Vercel terminates TLS
at its proxy and passes the assigned container port through `$PORT`.

Target production domain:

`https://dental-clinic-vn.vercel.app`

## 1. Required production variables

Add these under Vercel → Project → Settings → Environment Variables for both
Production and Preview. Never put secrets in `vercel.json` or commit them to Git.

| Variable | Purpose | Required |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | External SQL Server connection string | yes |
| `Jwt__Key` | Random JWT signing key of at least 32 characters | yes |
| `Jwt__Issuer` | For example, `DentalClinic` | yes |
| `Jwt__Audience` | For example, `DentalClinicClient` | yes |
| `CRON_SECRET` | Random secret protecting Vercel Cron endpoints | yes |
| `Gemini__ApiKey` | Chatbot API key | for AI chat |
| `ElevenLabs__ApiKey` | Text-to-speech API key | optional |
| `ElevenLabs__VoiceId` | Voice identifier | optional |
| `Scheduling__TimeZoneId` | Clinic timezone; defaults to `Europe/Moscow` | recommended |
| `BackgroundJobs__CleanupEnabled` | Set `true` only when stale-request cancellation is wanted | optional |

The frontend and API are same-origin on the default deployment. If another origin
calls the API, add `AllowedOrigins__0=https://your-domain`.

## 2. Database and schema migration

Vercel runs the application but does not provide SQL Server inside the container.
Use an external managed SQL Server such as Azure SQL.

The EF migration chain is designed to upgrade the existing live schema in place:
the baseline migration is idempotent and later migrations add the production
hardening fields/indexes used by the current application. On every relational app
startup, `Program.cs` now runs `Database.MigrateAsync()` **before** `DbSeeder` and
before the app begins serving requests. This also applies on Vercel, so a newly
published image cannot silently run against an older schema.

Before the first migration of an existing production database:

1. Take a verified database backup.
2. Point `ConnectionStrings__DefaultConnection` at the managed SQL Server.
3. Deploy the application; startup applies pending EF migrations automatically.
4. Check `GET /health`; it should return HTTP 200 and `"status":"Healthy"`.
5. Verify the latest migration is recorded in `__EFMigrationsHistory`.

For a local/manual maintenance window you can still run `dotnet ef database update`.
Do not bypass backups before schema changes.

## 3. Deploying from GitHub

Recommended permanent workflow:

1. In Vercel, select **New Project → Import Git Repository**.
2. Connect `ViolettaNcl/DentalClinic`; use `main` as Production Branch and `.` as Root Directory.
3. Add the variables from section 1.
4. Create a Preview deployment and verify `/health`, the home page, authentication,
   appointment creation, and the chatbot microphone.
5. Promote or create a Production deployment after verification.

`vercel.json` rewrites all traffic to the `web` container service. Vercel provides
HTTPS automatically. `Program.cs` honors forwarded protocol/client information from
Vercel before rate limiting and authentication-sensitive request handling.

## 4. Cron jobs

`vercel.json` defines four daily maintenance jobs:

| Time (UTC) | Endpoint | Purpose |
|---|---|---|
| `06:00` | `/api/maintenance/reminders` | Remind patients about the following day's visits |
| `06:10` | `/api/maintenance/follow-ups` | Send one-time post-visit follow-ups |
| `06:15` | `/api/maintenance/cleanup` | Cancel stale pending requests only when explicitly enabled |
| `06:30` | `/api/maintenance/chat-retention` | Remove expired chat/IP-pseudonym data |

Vercel sends `Authorization: Bearer <CRON_SECRET>`. The maintenance endpoints return
401 when the secret is missing or wrong. Regular `BackgroundService` workers are
disabled on Vercel because containers may suspend between requests.

## 5. GitHub Actions and FTPS backup

- `.github/workflows/ci.yml` builds the app and runs tests on pushes and PRs.
- `.github/workflows/codeql.yml` analyzes C# and JavaScript.
- `.github/workflows/cd.yml` repeats the test gate, publishes the Docker image to GHCR,
  and keeps a backup FTPS deployment path to Somee.
- Vercel is the primary production host. FTPS is only a fallback while the Vercel
  migration is being fully verified.

The FTPS job reads only GitHub repository/environment secrets; their values are never
stored in the repository:

- `FTP_SERVER`
- `FTP_USERNAME`
- `FTP_PASSWORD`
- `FTP_SERVER_DIR`

If any of the four secrets is missing, the workflow reports the FTPS deployment as
skipped while CI, Vercel, and the GHCR image publication continue normally.

After Git Integration is connected, Vercel creates Preview deployments for branches
and Production deployments from `main`.

## 6. Local Docker

```bash
cp .env.example .env
# fill in the DB password, JWT key, and API keys
docker compose up --build
```

The app is available at `http://localhost:8080`; SQL Server at `localhost:1433`.
Named Docker volumes retain the database across restarts.

## 7. Avatar persistence

New patient/admin avatars are stored durably in SQL and served through the authenticated
avatar endpoint. Legacy local avatar paths are cleaned up safely when replaced or
deleted. The application no longer depends on Vercel's ephemeral container filesystem
for newly uploaded avatars.

## 8. Post-deployment checklist

- [ ] The Production deployment is READY.
- [ ] `/health` returns HTTP 200 and reports a Healthy database.
- [ ] Pending EF migrations were applied and `__EFMigrationsHistory` contains the latest migration.
- [ ] The home page and static assets load over HTTPS.
- [ ] Patient/admin sign-in and registration work.
- [ ] Appointment creation rejects past dates and doctor conflicts.
- [ ] The microphone requests browser permission and inserts recognized text into chat.
- [ ] Cron endpoints return 401 without the correct `CRON_SECRET`.
- [ ] `BackgroundJobs__CleanupEnabled` is enabled only deliberately.
- [ ] Backups are configured for the production database.
- [ ] If FTPS backup is required, all four FTP secrets are configured in GitHub.
- [ ] Patient/admin avatar upload and retrieval survive a fresh deployment.
