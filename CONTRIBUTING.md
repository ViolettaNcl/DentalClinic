# Contributing to DentalClinic

Thank you for considering a contribution. DentalClinic handles authentication, clinic scheduling, patient data, and paid AI integrations, so changes are expected to preserve both product behavior and security boundaries.

## Before you start

1. Search existing [issues](https://github.com/ViolettaNcl/DentalClinic/issues) and pull requests.
2. For a substantial feature or schema change, open an issue first and describe the user problem, proposed scope, and migration impact.
3. Never use a public issue for a suspected vulnerability. Follow [SECURITY.md](SECURITY.md).

## Development workflow

```bash
git clone https://github.com/ViolettaNcl/DentalClinic.git
cd DentalClinic
git checkout -b type/short-description
dotnet tool restore
dotnet restore
npm install
```

Recommended branch prefixes are `feature/`, `fix/`, `docs/`, `test/`, and `chore/`.

Keep each pull request focused. Do not combine unrelated refactors, formatting changes, generated assets, and functional behavior in one review unless they are inseparable.

## Engineering expectations

- Follow the existing controller → service → EF Core boundaries; keep authorization at the HTTP boundary and invariants in reusable services/database constraints.
- Treat browser input, administrator-managed knowledge, provider output, uploaded files, and forwarded proxy headers as untrusted.
- Preserve cookie-based authentication: tokens must not be exposed to JavaScript, local storage, query strings, or logs.
- Make state-changing endpoints safe under retry and concurrency. Use transactions, conflict responses, and durable idempotency where the workflow requires them.
- Maintain all five UI locales (`ru`, `en`, `fr`, `el`, `ar`) and verify Arabic RTL behavior when visible copy or layout changes.
- Add or update tests for behavioral changes. A bug fix should normally include a regression test.
- Keep secrets and real patient/clinic data out of code, fixtures, screenshots, logs, and documentation.

## Verification

Run the checks relevant to your change before opening a pull request:

```bash
dotnet build DentalClinic.csproj --configuration Release
dotnet test DentalClinic.Tests/DentalClinic.Tests.csproj --configuration Release
npm run test:js
```

For browser-facing work:

```bash
npx playwright install chromium
BASE_URL=http://localhost:5192 npm run test:e2e
```

Changing `BASE_URL` does not change all canonical-URL assertions; some still expect the production domain.

Also review the [QA checklist](docs/QA_CHECKLIST.md) for responsive, localization, and accessibility coverage.

## Database changes

This checkout has migration classes but no tracked EF model snapshot. Establish a reviewed baseline against the existing schema before scaffolding from an EF model change; otherwise generated migrations may try to recreate existing tables. See the [developer guide](docs/en/DEVELOPER_GUIDE.md).

After that review:

```bash
dotnet ef migrations add DescriptiveMigrationName
dotnet ef migrations list --no-connect
```

Migration `Up` paths must fail safely on incompatible production data. Do not silently delete, coerce, or overwrite existing records merely to make a migration pass. Document backup and rollback expectations in the pull request.

## Documentation changes

English is canonical at `README.md` and `docs/en/`. Russian translations live at `README.ru.md` and the corresponding files directly under `docs/`.

When behavior changes, update the relevant English reference and its Russian counterpart in the same pull request. Keep commands copyable, distinguish current behavior from plans, and avoid claims that are not enforced by code or CI.

## Pull request checklist

- [ ] The pull request explains the problem, approach, risk, and verification evidence.
- [ ] New behavior has automated coverage or a clear reason why it cannot.
- [ ] Database/configuration/deployment impacts are documented.
- [ ] Security, privacy, localization, accessibility, and concurrency were considered.
- [ ] No secret, production credential, real patient data, or environment-specific artifact is included.
- [ ] Documentation matches the final implementation.

By contributing, you agree that your contribution is licensed under the repository's [MIT License](LICENSE).
