[⬅ Back to README](../../README.en.md)

# 🚀 Deployment

*[🇷🇺 Русская версия](../DEPLOYMENT.md)*

## 1. Publishing a build

```bash
dotnet publish -c Release -o ./publish
```

The `./publish` folder contains everything needed to run the app: the compiled
application, `wwwroot/` static assets, and `appsettings.json` (don't forget — on the
server it must contain **real production values**, set not through the file but through
environment variables — see below).

## 2. Configuration via environment variables (recommended for production)

ASP.NET Core lets you override any value from `appsettings.json` with an environment
variable, using `__` instead of `:`, e.g.:

```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=..."
export Jwt__Key="a-long-random-string-at-least-32-chars"
export Gemini__ApiKey="..."
```

This is the safest way to store secrets on a server — they don't sit in a file next to
the code.

## 3. Hosting options

The project is a standard ASP.NET Core application, so any of these work:

| Option | Notes |
|---|---|
| IIS / Windows Server | The classic .NET option, needs the ASP.NET Core Hosting Bundle module |
| Docker container | Use `dotnet publish` + the official `mcr.microsoft.com/dotnet/aspnet:9.0` image |
| Linux + Nginx (reverse proxy) | `dotnet DentalClinic.dll` as a systemd service behind Nginx |
| Managed hosting (Azure App Service, Railway, Render, etc.) | Usually just set environment variables in the hosting panel |

The project already includes a ready-made FTP publish profile
(`Properties/PublishProfiles/FTPProfile.pubxml`) — used for hosting on `somee.com`.
**The `FTPProfile.pubxml.user` file may contain saved FTP credentials — check this before
publishing the repository** (see `docs/en/SECURITY.md`).

## 4. HTTPS/HSTS configuration

In `Program.cs`, `UseHsts()` and `UseHttpsRedirection()` are only enabled outside
Development mode — on a real server with a valid TLS certificate (e.g. via Let's
Encrypt / Nginx / your hosting provider) this works out of the box.

## 5. Production database

1. Create the database on your SQL Server (or use a managed service).
2. Set the connection string via an environment variable (see section 2).
3. Apply migrations:
   ```bash
   dotnet ef database update --connection "your_production_connection_string"
   ```
   or run this once locally with a temporarily set production connection string.

## 6. Automated deployment (CI/CD)

Besides `ci.yml` (build + tests on every push/PR), the project has
`.github/workflows/cd.yml`. It runs automatically after a successful CI run on
`main` and does three things:

| Job | What it does | Secrets needed (Settings → Secrets and variables → Actions) |
|---|---|---|
| `test` | Re-runs `dotnet test` as a safety gate before any deploy — this also protects manual runs (`workflow_dispatch`), which otherwise would skip the CI check entirely | — |
| `deploy-ftp` | `dotnet publish` + upload over FTPS to the current host (somee.com) | `FTP_SERVER`, `FTP_USERNAME`, `FTP_PASSWORD`, `FTP_SERVER_DIR` (e.g. `/www.Dental-Clinic.somee.com`) |
| `docker-image` | Builds a Docker image and publishes it to `ghcr.io/<repo owner>/dentalclinic` | none — uses the built-in `GITHUB_TOKEN` |

`deploy-ftp` and `docker-image` both depend on `test` (`needs: test`) — if tests
fail, nothing gets deployed or published, regardless of what triggered the run.
If the FTP secrets aren't set, `deploy-ftp` simply fails at the upload step —
that's harmless to the repository and the current hosting.

Run both jobs manually (without waiting for a push) via
Actions → CD → Run workflow (`workflow_dispatch`).

## 7. Running via Docker / docker-compose

The project ships with a `Dockerfile` (multi-stage build) and a
`docker-compose.yml` that brings up the app together with SQL Server — no need
to install SQL Server locally.

```bash
cp .env.example .env
# open .env and fill in your values (DB password, JWT key, Gemini API key)

docker compose up --build
```

The app will be available at `http://localhost:8080`, SQL Server at
`localhost:1433`.

**Important:** there are no EF Core migrations in this project — tables in the
database are created manually (the same way you already do it for somee.com).
On first run against an empty database, `DbSeeder` only fills
`Services`/`Doctors` if the tables already exist — create the schema before
starting `app` for the first time.

SQL Server data and uploaded avatars are stored in named Docker volumes
(`dentalclinic-db-data`, `dentalclinic-uploads`) — they survive
`docker compose down` (but not `docker compose down -v`).

## 8. Monitoring: `/health`

The app exposes `GET /health` — no authentication, no rate limiting, meant
specifically for external monitoring (UptimeRobot, a Docker/orchestrator
healthcheck, curl from cron). It checks that the process is alive and that the
database connection works:

```json
{
  "status": "Healthy",
  "totalDurationMs": 12.4,
  "checks": [
    { "name": "db", "status": "Healthy", "durationMs": 12.1, "error": null }
  ]
}
```

`status: "Unhealthy"` and HTTP 503 mean the database is unreachable. Point your
external monitoring at this endpoint so you find out about downtime before
your patients do.

## 9. Pre-deployment checklist

- [ ] `appsettings.json` in the repository doesn't contain real secrets (see `docs/en/SECURITY.md`)
- [ ] `Jwt:Key` in production is a fresh random key, different from the one used in development
- [ ] `AllowedOrigins` points to the real frontend domain, not `localhost`
- [ ] `BackgroundJobs:CleanupEnabled` is deliberately enabled/disabled for your process
- [ ] Database backups are configured on the hosting provider's side
- [ ] If you use `cd.yml` — the `FTP_SERVER`/`FTP_USERNAME`/`FTP_PASSWORD`/`FTP_SERVER_DIR` secrets are set in Settings → Secrets and variables → Actions
- [ ] If you use Docker — tables in the container's database are created manually BEFORE the first `app` start (there are no migrations)
