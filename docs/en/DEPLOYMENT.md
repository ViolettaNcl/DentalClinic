[⬅ Back to README](../../README.en.md)

# 🚀 Deploying to Vercel

*[🇷🇺 Русская версия](../DEPLOYMENT.md)*

The project runs on Vercel as an ASP.NET Core 9 container service. Its deployment
configuration lives in `Dockerfile.vercel` and `vercel.json`. Vercel terminates TLS
at its proxy and passes the assigned container port through `$PORT`.

Target production domain:

`https://dental-clinic-violettancls-projects.vercel.app`

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

## 2. Database and leaving the previous host

Vercel runs the application but does not provide SQL Server inside the container.
Use an external managed SQL Server such as Azure SQL. To leave the old host while
preserving data:

1. Back up the current database.
2. Restore it into the new managed SQL Server.
3. After taking a backup, run `scripts/sql/20260901_stage1_appointments.sql`.
4. Set the new connection string in `ConnectionStrings__DefaultConnection`.
5. Check `GET /health`; it should return HTTP 200 and `"status":"Healthy"`.

The repository does not yet contain a complete EF Core migration history capable of
creating the entire schema from scratch. `DbSeeder` populates doctors and services but
expects the tables to exist. Do not point production at an empty database before
creating or migrating the schema.

## 3. Deploying from GitHub

Recommended permanent workflow:

1. In Vercel, select **New Project → Import Git Repository**.
2. Connect `ViolettaNcl/DentalClinic`; use `main` as Production Branch and `.` as Root Directory.
3. Add the variables from section 1.
4. Create a Preview deployment and verify `/health`, the home page, authentication,
   appointment creation, and the chatbot microphone.
5. Promote or create a Production deployment after verification.

`vercel.json` rewrites all traffic to the `app` container service. Vercel provides
HTTPS automatically. `Program.cs` honors `X-Forwarded-Proto`, avoiding redirect loops
behind the Vercel proxy.

## 4. Cron jobs

`vercel.json` defines two daily jobs compatible with the Hobby plan:

| Time (UTC) | Endpoint | Purpose |
|---|---|---|
| `06:00` | `/api/maintenance/reminders` | Remind patients about the following day's visits |
| `06:15` | `/api/maintenance/cleanup` | Cancel stale pending requests only when explicitly enabled |

Vercel sends `Authorization: Bearer <CRON_SECRET>`. Both endpoints return 401 when
the secret is missing or wrong. Regular `BackgroundService` workers are disabled on
Vercel because containers may suspend between requests.

## 5. GitHub Actions

- `.github/workflows/ci.yml` builds the app and runs tests on pushes and PRs.
- `.github/workflows/codeql.yml` analyzes C# and JavaScript.
- `.github/workflows/cd.yml` repeats the test gate and publishes the Docker image to GHCR.
- The FTP deployment and FTP publish profile have been removed.

After Git Integration is connected, Vercel creates Preview deployments for branches
and Production deployments from `main`.

## 6. Local Docker

```bash
cp .env.example .env
# fill in the DB password, JWT key, and API keys
docker compose up --build
```

The app is available at `http://localhost:8080`; SQL Server at `localhost:1433`.
Named Docker volumes retain the database and uploads across restarts.

## 7. File upload limitation

Avatars are currently written to `wwwroot/uploads/avatars`. A Vercel container's
filesystem is not persistent storage, so uploaded avatars may disappear after a new
deployment or instance replacement. Move this feature to Vercel Blob or another object
store before relying on it in production.

## 8. Post-deployment checklist

- [ ] The Production deployment is READY.
- [ ] `/health` returns HTTP 200 and reports a Healthy database.
- [ ] The home page and static assets load over HTTPS.
- [ ] Patient/admin sign-in and registration work.
- [ ] Appointment creation rejects past dates and doctor conflicts.
- [ ] The microphone requests browser permission and inserts recognized text into chat.
- [ ] Cron endpoints return 401 without the correct `CRON_SECRET`.
- [ ] `BackgroundJobs__CleanupEnabled` is enabled only deliberately.
- [ ] Backups are configured for the new database.
- [ ] Avatars use external object storage or the upload feature is temporarily disabled.
