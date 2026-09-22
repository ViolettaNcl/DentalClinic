# Developer guide

[Documentation](../README.md) · [Project README](../../README.md) · [Русский](../DEVELOPER_GUIDE.md)

## 1. Prerequisites

| Tool | Version / purpose |
|---|---|
| .NET SDK | 10.x; application, EF tooling, and .NET tests |
| SQL Server | 2019+ / Azure SQL / SQL Server container |
| Git | Source workflow |
| Node.js | 22 recommended; JS and Playwright tests |
| Docker Desktop | Optional Compose/container workflow |

External provider keys are optional for most local development, but Gemini-backed chat/translation and ElevenLabs speech require valid credentials when those routes are exercised.

## 2. Clone and configure

```bash
git clone https://github.com/ViolettaNcl/DentalClinic.git
cd DentalClinic
dotnet tool restore
dotnet restore
```

Create an ignored local configuration:

```bash
cp appsettings.Example.json appsettings.json
```

PowerShell equivalent:

```powershell
Copy-Item appsettings.Example.json appsettings.json
```

Replace every placeholder that your workflow uses. At minimum the application requires a reachable `ConnectionStrings:DefaultConnection`, a JWT key of at least 32 UTF-8 bytes, and matching issuer/audience values.

For local secrets, prefer the ASP.NET Core Secret Manager:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=..."
dotnet user-secrets set "Jwt:Key" "generate-a-long-random-development-secret"
dotnet user-secrets set "Jwt:Issuer" "DentalClinicLocal"
dotnet user-secrets set "Jwt:Audience" "DentalClinicLocalClient"
dotnet user-secrets set "Gemini:ApiKey" "development-key-if-needed"
```

`dotnet user-secrets init` adds a local project identifier to the project file. Do not commit that change unless the team intentionally standardizes a shared Secret Manager ID.

## 3. Create/update the database

The repository pins `dotnet-ef` in `.config/dotnet-tools.json`.

```bash
dotnet ef migrations list --no-connect
dotnet ef database update
```

Relational startup also calls `Database.MigrateAsync()` before serving requests. Explicitly running the update remains useful because migration failures appear before the web process starts and can be reviewed independently.

On an empty database, `DbSeeder` adds the starter doctors, service catalogue, and an initial sedation knowledge row. It does **not** create a default administrator. The first admin/super-admin must be provisioned through a controlled operator process or migration with a unique strong password; never add public default credentials to source or documentation.

## 4. Run the application

```bash
dotnet run
```

Launch profiles:

- `http://localhost:5192`
- `https://localhost:7063`
- Swagger in Development: `/swagger`
- Health check: `/health`

Development deliberately permits direct tooling requests and disables the production rate-limiter middleware. It is not representative of all production origin/quota behavior; security-sensitive changes need integration tests and, when appropriate, a staging/production-like verification.

## 5. Docker workflow

```bash
cp .env.example .env
# Replace all placeholders and keep .env untracked.
docker compose up --build
```

Compose starts SQL Server 2022 Express plus the application, uses named volumes for the database and legacy upload directory, and exposes the app on `http://localhost:8080`.

Useful commands:

```bash
docker compose ps
docker compose logs -f app
docker compose down
```

Do not use `docker compose down -v` against data you need to retain; `-v` deletes the named database volume.

## 6. Repository map

| Change | Primary locations |
|---|---|
| HTTP route/authorization | `Controllers/`, DTOs in `Models/`, API docs |
| Business invariant/workflow | `Services/` plus unit/integration tests |
| Schema/entity/index | `Models/`, `Data/ApplicationDbContext.cs`, `Migrations/`, data dictionary |
| Request pipeline/security boundary | `Program.cs`, `Middleware/`, `Filters/` |
| Realtime event | `Hubs/`, `NotificationService`, `wwwroot/assets/js/services/realtime.js` |
| Background/cron behavior | `BackgroundJobs/`, `AppointmentMaintenanceService`, `MaintenanceController`, `vercel.json` |
| Public page | `wwwroot/index.html` or `wwwroot/pages/`, page CSS, public manager |
| Dashboard behavior | `wwwroot/assets/js/managers/{patient,admin}/` and dashboard HTML/CSS |
| Shared browser infrastructure | `wwwroot/assets/js/core/` or `services/` |
| UI copy | Every relevant file under `wwwroot/assets/i18n/` |

See [Architecture](ARCHITECTURE.md) for the component and trust-boundary model.

## 7. Database changes

Review the existing migration strategy before creating a migration. This checkout has explicit migration classes but no tracked EF model snapshot; blindly scaffolding from an empty baseline can generate duplicate table creation. Establish and review a baseline snapshot against the existing schema before using the usual command:

```bash
dotnet ef migrations add DescriptiveMigrationName
dotnet ef migrations list --no-connect
dotnet ef database update
```

Review generated SQL/operations before applying them to shared data. Existing migrations use fail-closed checks for incompatible legacy rows rather than silently deleting or rewriting clinic records.

Migration checklist:

- preserve or explicitly migrate existing data;
- add DB constraints for closed domains and invariants that must survive all writers;
- index bounded production query paths;
- make `Down` behavior explicit and do not present destructive rollback as lossless;
- document backup requirements in the deployment guide/PR.

## 8. Authentication and same-origin development

The built-in browser flow stores JWTs in the `dc_auth` `HttpOnly` cookie. Do not reintroduce local storage, session-storage tokens, query-string tokens, or a response-body token.

Use `credentials: 'include'`/same-origin browser requests. Production unsafe mutations require a matching `Origin` or `Referer`; paid AI routes require an accepted `Origin`. The included Postman collection relies on its cookie jar and is easiest to use against Development.

Password requirements are centralized in `PasswordPolicy`: at least 8 characters with uppercase, lowercase, digit, and special character.

## 9. Tests

### .NET

```bash
dotnet test DentalClinic.Tests/DentalClinic.Tests.csproj --configuration Release
```

`CustomWebApplicationFactory` boots the real application pipeline and replaces SQL Server with a unique EF Core InMemory database. Unit tests cover policies and transformations; integration tests cover route security, ownership, state changes, and error semantics.

InMemory tests do not reproduce SQL Server locking, filtered indexes, check constraints, or provider SQL behavior. Migration-shape tests and CI migration discovery reduce that gap, but schema/concurrency changes still need SQL Server-aware review.

### JavaScript

```bash
npm install
npm run test:js
```

The Node test suite protects browser/session, rendering, localization, Denta, dashboard, deployment-policy, and UI contracts.

### Playwright

```bash
npx playwright install chromium
BASE_URL=http://localhost:5192 npm run test:e2e
```

Without `BASE_URL`, E2E targets the public deployment. Some canonical-URL assertions still expect the production domain even when `BASE_URL` changes; a local run is therefore not an environment-independent acceptance suite. The suite includes desktop/mobile flows and axe-core checks. Never point destructive or credentialed tests at production unless the test is explicitly designed and approved for that environment.

### CI parity

CI restores/builds the app, builds the Vercel container, verifies migration discovery, runs .NET tests, and runs JS tests. Separate workflows run CodeQL, production smoke checks, Playwright, optional live-provider smoke, and Vercel registry retention.

## 10. Common change workflows

### Add an endpoint

1. Define a bounded request DTO; do not bind an EF entity directly for mutations.
2. Add role and ownership checks at the controller boundary.
3. Move reusable/concurrent business logic into a service.
4. Add database constraints when the invariant must hold outside that action.
5. Add integration tests for success, validation, unauthorized, forbidden, conflict, and cancellation paths.
6. Update the English and Russian API docs.

### Add a UI language key

1. Add the same key to `ru.json`, `en.json`, `fr.json`, `el.json`, and `ar.json`.
2. Run `npm run test:js` to catch parity/usage errors.
3. Verify Arabic directionality and layout, not only translated text.

### Add a paid provider call

1. Keep the API key server-side and use a dedicated typed boundary/handler.
2. Add payload bounds, cancellation, deterministic disposal, timeouts, and redacted logging.
3. Extend same-origin route policy and both local/distributed quota policies.
4. Define provider failure mapping and a user-safe fallback.
5. Add tests that prove keys cannot leak through URLs, responses, or logs.

## 11. Troubleshooting

### Startup says the connection string or JWT key is missing

Verify the effective configuration source. Environment variables use double underscores, for example `Jwt__Key` and `ConnectionStrings__DefaultConnection`.

### HTTPS development certificate warning

```bash
dotnet dev-certs https --trust
```

Alternatively use the HTTP launch profile for local-only work.

### `dotnet ef` is unavailable

Run `dotnet tool restore` from the repository root, then `dotnet ef --version`.

### Chat or translation fails

Confirm `Gemini:ApiKey`, allowed origin behavior, request bounds, and provider quota. Denta can still answer some deterministic clinic questions without Gemini; provider-generated requests cannot.

### Login works in the browser but not an API tool

The login response sets a cookie instead of returning a token. Enable the tool's cookie jar, keep the same base URL/port, and use Development or provide a correct same-origin `Origin` header in production-like environments.

### SQL Server startup/migration failure

Check network reachability, credentials, TLS/trust settings, migration history, and database permissions. Do not solve a migration conflict by deleting production data; back up the database and resolve the conflicting invariant explicitly.

## 12. Before opening a pull request

Follow [CONTRIBUTING.md](../../CONTRIBUTING.md), run the relevant automated checks, update both documentation languages, and review [SECURITY.md](SECURITY.md) when the change touches identity, patient data, uploads, external providers, or deployment.
