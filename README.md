<div align="center">

<img src="docs/assets/readme-hero.svg" alt="DentalClinic — production-grade dental clinic platform" width="100%">

<p>
  <strong>A dental clinic platform engineered like a product, not a demo.</strong><br>
  <sub>Online booking · appointment CRM · realtime notifications · secure sessions · in-page routes · AI assistant · 5 languages</sub>
</p>

<p>
  <a href="README.md"><img src="https://img.shields.io/badge/English-13B39B?style=for-the-badge" alt="English"></a>
  <a href="README.ru.md"><img src="https://img.shields.io/badge/Русский-0A8A77?style=for-the-badge" alt="Русский"></a>
</p>

<p>
  <a href="https://dental-clinic-vn.vercel.app/"><img src="https://img.shields.io/badge/Live_demo-13B39B?style=for-the-badge&logo=vercel&logoColor=white" alt="Live demo"></a>
  <a href="docs/README.md"><img src="https://img.shields.io/badge/Docs-0A8A77?style=for-the-badge&logo=readthedocs&logoColor=white" alt="Documentation"></a>
  <a href="docs/en/ARCHITECTURE.md"><img src="https://img.shields.io/badge/Architecture-0A534A?style=for-the-badge&logo=diagramsdotnet&logoColor=white" alt="Architecture"></a>
  <a href="docs/en/API.md"><img src="https://img.shields.io/badge/API-FFB672?style=for-the-badge&logo=swagger&logoColor=111111" alt="API"></a>
  <a href="docs/en/SECURITY.md"><img src="https://img.shields.io/badge/Security-111827?style=for-the-badge&logo=letsencrypt&logoColor=white" alt="Security"></a>
</p>

<p>
  <a href="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/ci.yml"><img src="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/e2e.yml"><img src="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/e2e.yml/badge.svg" alt="Production E2E"></a>
  <a href="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/codeql.yml"><img src="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/codeql.yml/badge.svg" alt="CodeQL"></a>
  <a href="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/production-smoke.yml"><img src="https://github.com/ViolettaNcl/DentalClinic/actions/workflows/production-smoke.yml/badge.svg" alt="Production smoke"></a>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-2ea44f.svg" alt="MIT License"></a>
</p>

<p>
  <a href="#showcase">Showcase</a> ·
  <a href="#features">Features</a> ·
  <a href="#whats-new">What's new</a> ·
  <a href="#architecture">Architecture</a> ·
  <a href="#quality">Quality</a> ·
  <a href="#quick-start">Quick start</a> ·
  <a href="#documentation">Docs</a>
</p>

</div>

<img src="docs/assets/readme-divider.svg" width="100%" alt="" aria-hidden="true">

<a id="showcase"></a>
## 🎬 Showcase

<p align="center">
  <img src="docs/assets/readme-showcase.svg" alt="Animated showcase of the DentalClinic home, services, doctors and contact pages with a phone preview" width="100%">
</p>

<p align="center"><sub>Real screens from the current build: home, service catalogue, doctor profiles and a route built inside the contact page — with the mobile layout alongside.</sub></p>

<a id="features"></a>
## ✨ What you get

<table>
<tr>
<td width="33%" valign="top">

### 🌐 Public site
- Service catalogue with live prices
- Doctor profiles and one-click booking
- Guest appointment requests
- **In-page routes** (car · walk · transit)
- Reviews with moderation and translation
- 5 languages incl. Arabic **RTL**

</td>
<td width="33%" valign="top">

### 👤 Patient workspace
- Registration and secure cookie sessions
- Appointment history and status
- Reschedule / cancel within clinic rules
- Profile, avatar, password change
- Reviews after completed visits
- **Realtime** notifications (SignalR)

</td>
<td width="33%" valign="top">

### 🏥 Clinic operations
- Appointment CRM and phone bookings
- Doctors, services and schedules
- Availability-aware calendar
- Analytics, XLSX and printable reports
- Multi-admin with super-admin safeguards
- Reminders and follow-ups automation

</td>
</tr>
<tr>
<td valign="top">

### 🤖 Denta assistant
- Answers from SQL-backed clinic facts first
- Gemini structured output for the rest
- Allow-listed links, safe booking hand-off
- Healthcare-safety rules, optional voice

</td>
<td valign="top">

### 🛡 Security by design
- JWT in `HttpOnly` + `SameSite=Strict` cookies
- Per-token logout, token-version revocation
- Same-origin checks, CORS allow-list
- Rate limits + SQL-backed distributed quotas

</td>
<td valign="top">

### ✨ Polished UX
- Motion layer: reveals, tilt, ripple, glow
- Symmetric header, mobile-first layouts
- No horizontal scroll from 320 px to 4K
- Honours `prefers-reduced-motion`

</td>
</tr>
</table>

<a id="whats-new"></a>
## 🚀 What's new

| | Change | Why it matters |
|:-:|---|---|
| 🔐 | **Reliable patient sign-in across instances.** Logout revokes only the current token (`jti`); database latency during session validation returns a retryable `503` instead of `401`. | Fresh logins and registrations are no longer rejected by another Vercel instance, and slow queries never log a patient out. |
| 🗺 | **Routes inside the contact page.** The map moved to the Yandex Maps widget with real driving, walking and transit directions, plus an "open in navigator" link. | The legacy Google embed was no longer framable and rendered an empty map. |
| ✨ | **Site-wide motion layer.** Scroll reveals, hero particles and cursor glow, 3D card tilt, ripple and shine, animated counters and modals. | A premium feel with one CSS file and one deferred script — no frameworks. |
| 📐 | **Symmetric header and responsive hardening.** Centred desktop navigation; header controls work from the first paint. | Consistent layout on every screen; fixed an E2E race on the language switcher. |
| ⚡ | **Cache-safe static assets.** HTML, JS, CSS and i18n revalidate via `ETag`. | Visitors never run a stale mix of ES modules after a deploy. |

Full history: [CHANGELOG.md](CHANGELOG.md).

<img src="docs/assets/readme-divider.svg" width="100%" alt="" aria-hidden="true">

<a id="architecture"></a>
## 🏗 Architecture

<p align="center">
  <img src="docs/assets/readme-architecture.svg" alt="Animated architecture diagram: browser, ASP.NET Core 10 modular monolith, SQL Server, SignalR, Gemini, ElevenLabs, Yandex Maps and Vercel Cron" width="100%">
</p>

One deployable **ASP.NET Core 10 modular monolith** owns the static frontend, REST API, SignalR hub, background workflows and database access. Controllers handle HTTP concerns, domain services own the rules, and SQL constraints are the last line of defence — so no single layer is the only guard.

<details>
<summary><strong>Request and session flow (Mermaid)</strong></summary>

```mermaid
sequenceDiagram
    participant B as Browser
    participant A as ASP.NET Core
    participant C as Token cache
    participant D as SQL Server
    B->>A: POST /api/auth/login
    A->>D: Account + BCrypt hash
    A-->>B: Set-Cookie dc_auth (HttpOnly, SameSite=Strict)
    B->>A: GET /api/... (cookie)
    A->>C: Revoked jti? Token version cached?
    alt cache miss
        A->>D: Current token version
        D-->>A: version
    end
    A-->>B: 200 data · 401 revoked · 503 retry when DB is slow
```

</details>

Deep dive: [architecture guide](docs/en/ARCHITECTURE.md) — trust boundaries, request flows and design decisions.

## 🧰 Technology stack

| Layer | Stack |
|---|---|
| **Backend** | ![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white) ![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core_10-512BD4?logo=dotnet&logoColor=white) ![SignalR](https://img.shields.io/badge/SignalR-0A534A) ![Hosted services](https://img.shields.io/badge/Hosted_services-0A8A77) |
| **Data** | ![EF Core](https://img.shields.io/badge/EF_Core_10-512BD4?logo=dotnet&logoColor=white) ![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?logo=microsoftsqlserver&logoColor=white) ![Migrations](https://img.shields.io/badge/Code--first_migrations-0A534A) |
| **Security** | ![JWT](https://img.shields.io/badge/JWT-000000?logo=jsonwebtokens&logoColor=white) ![BCrypt](https://img.shields.io/badge/BCrypt-0A8A77) ![CodeQL](https://img.shields.io/badge/CodeQL-2F363D?logo=github&logoColor=white) |
| **Frontend** | ![HTML5](https://img.shields.io/badge/HTML5-E34F26?logo=html5&logoColor=white) ![CSS3](https://img.shields.io/badge/CSS3-1572B6?logo=css&logoColor=white) ![JavaScript](https://img.shields.io/badge/ES_modules-F7DF1E?logo=javascript&logoColor=111) ![i18n](https://img.shields.io/badge/i18n-5_languages_·_RTL-13B39B) |
| **Integrations** | ![Gemini](https://img.shields.io/badge/Google_Gemini-8E75B2?logo=googlegemini&logoColor=white) ![ElevenLabs](https://img.shields.io/badge/ElevenLabs-000000) ![Yandex Maps](https://img.shields.io/badge/Yandex_Maps-FC3F1D) |
| **Delivery** | ![Docker](https://img.shields.io/badge/Docker-2496ED?logo=docker&logoColor=white) ![Vercel](https://img.shields.io/badge/Vercel-000000?logo=vercel&logoColor=white) ![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-2088FF?logo=githubactions&logoColor=white) ![GHCR](https://img.shields.io/badge/GHCR-181717?logo=github&logoColor=white) |
| **Testing** | ![xUnit](https://img.shields.io/badge/xUnit-512BD4) ![Node test runner](https://img.shields.io/badge/node:test-339933?logo=nodedotjs&logoColor=white) ![Playwright](https://img.shields.io/badge/Playwright-2EAD33?logo=playwright&logoColor=white) ![axe-core](https://img.shields.io/badge/axe--core-663399) |

<a id="quality"></a>
## ✅ Quality gates

<table>
<tr>
<td align="center" width="25%"><h3>488</h3><sub>.NET unit &amp; integration tests</sub></td>
<td align="center" width="25%"><h3>200</h3><sub>JavaScript regression tests</sub></td>
<td align="center" width="25%"><h3>19</h3><sub>Playwright E2E checks<br>desktop + mobile</sub></td>
<td align="center" width="25%"><h3>1 h · 6 h</h3><sub>scheduled production<br>smoke · E2E</sub></td>
</tr>
</table>

| Workflow | What it proves |
|---|---|
| **CI** | Release build and the full .NET test suite on every push |
| **Production E2E** | Health, canonical URLs, accessibility (axe), languages + RTL, horizontal overflow, chat — on desktop and mobile |
| **CodeQL** | Static security analysis for C# and JavaScript |
| **Production smoke** | Scheduled health and public-route checks of the live site |
| **CD** | Test gate, GHCR image publication, optional FTPS fallback delivery |

<img src="docs/assets/readme-divider.svg" width="100%" alt="" aria-hidden="true">

<a id="quick-start"></a>
## ⚡ Quick start

> **Prerequisites:** [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) · SQL Server 2019+ or Azure SQL · Git · optional Node.js 22 (tests) and Docker Desktop

```bash
git clone https://github.com/ViolettaNcl/DentalClinic.git
cd DentalClinic
cp appsettings.Example.json appsettings.json   # PowerShell: Copy-Item appsettings.Example.json appsettings.json
dotnet tool restore && dotnet restore
dotnet ef database update
dotnet run --launch-profile http                # → http://localhost:5192 · Swagger at /swagger
```

Replace the example database, JWT, origin and clinic values in `appsettings.json` first — real secrets must never be committed.

<details>
<summary><strong>🐳 Run with Docker Compose</strong></summary>

```bash
cp .env.example .env      # replace every placeholder
docker compose up --build # → http://localhost:8080 · SQL Server on localhost:1433
```

</details>

<details>
<summary><strong>🧪 Run the test suites</strong></summary>

```bash
dotnet test DentalClinic.Tests/DentalClinic.Tests.csproj --configuration Release
npm install
npm run test:js
npx playwright install chromium
npm run test:e2e                      # targets the live site by default
BASE_URL=http://localhost:5192 npx playwright test   # or a local/approved environment
```

</details>

<details>
<summary><strong>⚙️ Configuration reference</strong></summary>

Environment variables use `__` for nested keys, e.g. `Jwt__Key`.

| Setting | Purpose | Requirement |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection | Required |
| `Jwt__Key` | HMAC signing secret, ≥ 32 UTF-8 bytes | Required |
| `Jwt__Issuer`, `Jwt__Audience` | Token validation boundaries | Required |
| `AllowedOrigins__0` | Trusted browser origin | Outside same-origin defaults |
| `Clinic__Phone`, `__Email`, `__Address`, `__Hours` | Public contact details shown on the site and map | Required for production content |
| `Clinic__Latitude`, `Clinic__Longitude` | Exact map pin and local ETA estimate | Recommended |
| `Scheduling__*` | Time zone, opening hours, slot interval, lead time | Recommended |
| `Gemini__ApiKey` | Denta and review translation | Required for AI features |
| `ElevenLabs__ApiKey` | Voice responses | Optional |
| `CRON_SECRET` | Vercel maintenance endpoint authentication | Required on Vercel |

See [Security](docs/en/SECURITY.md) before configuring any shared or production environment.

</details>

## 🧭 Operational boundaries

- **Medical safety** — Denta and the Smile Meter are informational, never diagnostic or treatment-planning tools.
- **Operations** — deployment needs operator-managed secrets, an external database and separately provisioned administrators.
- **Known limits** — no administrator MFA or password recovery yet; multi-instance SignalR needs a backplane; logout revocation is per instance. Details in [Security](docs/en/SECURITY.md).
- **Deployment status** — Vercel Git deployments are paused in `vercel.json`; production is deployed manually, while GitHub Actions still run on every push. See the [deployment guide](docs/en/DEPLOYMENT.md).

<a id="documentation"></a>
## 📚 Documentation

| | Guide | English | Русский |
|:-:|---|:-:|:-:|
| 🏗 | Architecture — components, trust boundaries, data flow | [Open](docs/en/ARCHITECTURE.md) | [Открыть](docs/ARCHITECTURE.md) |
| 📡 | REST API — routes, access rules, limits, errors | [Open](docs/en/API.md) | [Открыть](docs/API.md) |
| 🗃 | Data dictionary — entities, constraints, retention | [Open](docs/en/DATA_DICTIONARY.md) | [Открыть](docs/DATA_DICTIONARY.md) |
| 👨‍💻 | Developer guide — setup, migrations, tests | [Open](docs/en/DEVELOPER_GUIDE.md) | [Открыть](docs/DEVELOPER_GUIDE.md) |
| 🚀 | Deployment — Vercel, Docker, cron, release checks | [Open](docs/en/DEPLOYMENT.md) | [Открыть](docs/DEPLOYMENT.md) |
| 🔒 | Security — controls, secrets, residual risks | [Open](docs/en/SECURITY.md) | [Открыть](docs/SECURITY.md) |
| 👤 | User guide — visitors and patients | [Open](docs/en/USER_GUIDE.md) | [Открыть](docs/USER_GUIDE.md) |
| 👑 | Administrator guide — clinic and super-admin | [Open](docs/en/ADMIN_GUIDE.md) | [Открыть](docs/ADMIN_GUIDE.md) |

Start at the [documentation hub](docs/README.md). Contributions: [CONTRIBUTING.md](CONTRIBUTING.md) · plans: [ROADMAP.md](ROADMAP.md) · vulnerabilities: [SECURITY.md](SECURITY.md) · history: [CHANGELOG.md](CHANGELOG.md).

<br>

<img src="docs/assets/readme-footer.svg" width="100%" alt="DentalClinic — engineered with care · MIT licensed">

<div align="center">
<sub>Built and maintained by <a href="https://github.com/ViolettaNcl">Violetta Nicolaou</a> · released under the <a href="LICENSE">MIT License</a> · if this project helped you, a ⭐ is appreciated.</sub>
</div>
