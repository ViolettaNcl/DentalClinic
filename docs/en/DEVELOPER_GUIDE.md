[⬅ Back to README](../../README.en.md)

# 👨‍💻 Developer Guide

*[🇷🇺 Русская версия](../DEVELOPER_GUIDE.md)*

## 1. Requirements

- [.NET SDK 9](https://dotnet.microsoft.com/download) (`dotnet --version` → 9.x)
- SQL Server (local, in Docker, or cloud-hosted — e.g. [somee.com](https://somee.com),
  Azure SQL, or local SQL Server Express / LocalDB)
- A Google Gemini API key (for the chatbot) — get one at
  [ai.google.dev](https://ai.google.dev)
- (Optional) an ElevenLabs API key — for voicing the bot's replies

## 2. Installation and first run

```bash
git clone https://github.com/ViolettaNcl/DentalClinic.git
cd DentalClinic

# 1. Create your own appsettings.json from the template
cp appsettings.Example.json appsettings.json
# open appsettings.json and fill in your own values (DB connection string, JWT key, API keys)

# 2. Restore dependencies
dotnet restore

# 3. Apply EF Core migrations (creates tables in your database)
dotnet ef database update

# 4. Run the app
dotnet run
```

By default, the site will be available at the address from
`Properties/launchSettings.json` (usually `https://localhost:7063` and
`http://localhost:5192`). In Development mode, Swagger UI is also available at
`https://localhost:7063/swagger`.

On first run against an empty database, `DbSeeder` automatically populates the
`Services` and `Doctors` tables with starter data — you can edit them afterward through
the admin panel.

## 3. Handling secrets (important!)

**Never commit `appsettings.json` with real values.** The recommended approach for local
development is `dotnet user-secrets`:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your_connection_string"
dotnet user-secrets set "Jwt:Key" "a_long_random_string"
dotnet user-secrets set "Gemini:ApiKey" "your_key"
```

Secrets from `user-secrets` are automatically picked up by `IConfiguration` during
development and **never end up in the repository**. See
[`docs/en/SECURITY.md`](SECURITY.md) for details.

## 4. Project structure

See the "Repository layout" section in
[`docs/en/ARCHITECTURE.md`](ARCHITECTURE.md) — it lays out what each folder is for.

In short, if you're adding new functionality:

| What you're adding | Where to look |
|---|---|
| A new REST endpoint | `Controllers/` — create a controller or add a method to an existing one |
| A new database entity | `Models/` (class) + `Data/ApplicationDbContext.cs` (`DbSet<>`, indexes) + a migration |
| Business logic / an integration | `Services/` |
| A background job | `BackgroundJobs/` — a `BackgroundService` subclass, register it in `Program.cs` |
| A frontend page | `wwwroot/pages/*.html` + styles in `wwwroot/assets/css/pages/` + logic in `wwwroot/assets/js/managers/` |
| A UI translation | add the key to **every** file in `wwwroot/assets/i18n/*.json` |

## 5. Database migrations

After changing the data model (a class in `Models/` or `ApplicationDbContext`):

```bash
dotnet ef migrations add YourMigrationName
dotnet ef database update
```

Requires the `dotnet-ef` tool:

```bash
dotnet tool install --global dotnet-ef
```

## 6. Frontend: JS architecture

The frontend has no build tools or frameworks — just plain ES modules:

- `assets/js/core/` — cross-cutting infrastructure: i18n, chatbot, language switcher,
  navigation, header notifications;
- `assets/js/services/` — low-level services: `apiClient.js` (a `fetch` wrapper with
  JWT), `realtime.js` (a SignalR client wrapper), `dateUtils.js`;
- `assets/js/managers/` — page-specific logic, split by role: `public/` (public pages),
  `patient/` (patient dashboard), `admin/` (admin panel).

Styles follow a structure close to ITCSS: `base/` (variables, reset) → `layout/`
(header/footer) → `components/` (reusable blocks) → `pages/` (page-specific styles).

## 7. Testing locally

The project doesn't yet have automated tests — when adding new functionality, it's
recommended to:
1. Verify endpoints via Swagger UI (`/swagger`) or the `DentalClinic.http` file (you can
   open and run requests directly in Visual Studio / VS Code with the REST Client
   extension).
2. Manually check the UI for all three roles (guest, patient, admin).

## 8. Troubleshooting

**The app won't start — "Jwt:Key не задан в конфигурации" error.**
You haven't created `appsettings.json` (or set the key via `user-secrets`/environment
variables). See section 3 above.

**SQL Server connection error during `dotnet ef database update`.**
Check that the connection string in `ConnectionStrings:DefaultConnection` is correct and
the DB server is reachable (for local SQL Server Express, it's usually
`Server=(localdb)\mssqllocaldb;Database=DentalClinic;Trusted_Connection=True;`).

**The AI chat doesn't respond / returns an error.**
Make sure `Gemini:ApiKey` is set and valid — without it, `ChatController` can't reach the
Gemini API. Voice replies (TTS) are optional: without `ElevenLabs:ApiKey` there's simply
no audio, but the bot's text replies keep working.

## 9. Extending the project

**Adding a new UI language:**
1. Create `wwwroot/assets/i18n/<language-code>.json` with all keys, mirroring `ru.json`.
2. Add the language to the list in `languageSwitcher.js`.
3. If doctor name translations are needed, add a `FullName<Code>` field to the `Doctor`
   model and the `Doctors` table (requires a migration).

**Changing prices without touching code:**
All prices live in the `Services` table, editable from the admin panel. The AI bot picks
up changes automatically, without a server restart — see `ChatKnowledgeService`.
