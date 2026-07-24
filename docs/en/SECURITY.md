[⬅ Back to README](../../README.en.md)

*[🇷🇺 Русская версия](../SECURITY.md)*

# 🔒 Security

This document describes the protections already implemented in the project and how
secrets (passwords, API keys) are managed.

## Secrets management

Real values (the DB connection string, the JWT signing key, the Gemini/ElevenLabs API
keys) are never stored in the repository. Instead:

- the repository ships [`appsettings.Example.json`](../../appsettings.Example.json) — a
  template with test placeholders instead of real values;
- the actual `appsettings.json` with real values is created locally by whoever deploys
  the project, and is listed in `.gitignore` — it never reaches git;
- for local development, values are conveniently stored via `dotnet user-secrets` (see
  [`docs/en/DEVELOPER_GUIDE.md`](DEVELOPER_GUIDE.md), section 3);
- in production, they're set via the hosting provider's environment variables (see
  [`docs/en/DEPLOYMENT.md`](DEPLOYMENT.md), section 2).

## Implemented protections

| Measure | Where it's implemented | What it provides |
|---|---|---|
| Password hashing (BCrypt) | `AuthController`, `Models/Patient.cs`, `Models/Admin.cs` | Passwords are never stored or transmitted in plain text |
| JWT with expiry validation | `JwtTokenService`, `Program.cs` (`ClockSkew = TimeSpan.Zero`) | Tokens can't be used indefinitely or with time "slack" |
| Rate limiting | `Program.cs`, `auth`/`appointment`/`chat`/`translate` policies | Protection against password brute-forcing and running up costs on the paid AI API |
| CORS allow-list | `Program.cs`, `AllowedOrigins` in configuration | The API won't accept requests from arbitrary sites |
| Global exception handler | `Program.cs` | Internal error details don't leak into the client response |
| Input validation | Data Annotations in `Models/*.cs` | Length/format constraints (e.g. phone, email) are checked before hitting the database |

## Known limitations (for honest context)

- Rate limiting is in-process, per instance — deploying across multiple instances would
  need a shared store (e.g. Redis).
- No "second admin / super-admin" role — only a flat patient/admin split.
- Avatar uploads are stored on the server's local disk rather than object storage — see
  [`docs/en/DEPLOYMENT.md`](DEPLOYMENT.md) when scaling to multiple servers.

## Found a vulnerability?

Report it via Issues in the repository, including reproduction steps.
