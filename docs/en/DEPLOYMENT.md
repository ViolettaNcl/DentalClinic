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

## 6. Pre-deployment checklist

- [ ] `appsettings.json` in the repository doesn't contain real secrets (see `docs/en/SECURITY.md`)
- [ ] `Jwt:Key` in production is a fresh random key, different from the one used in development
- [ ] `AllowedOrigins` points to the real frontend domain, not `localhost`
- [ ] `BackgroundJobs:CleanupEnabled` is deliberately enabled/disabled for your process
- [ ] Database backups are configured on the hosting provider's side
