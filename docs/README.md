# DentalClinic documentation

[Project README](../README.md) · [Русская версия](README.ru.md)

This page is the entry point for project documentation. Documents under `docs/en/` describe the current implementation; operational snapshots and historical notes are listed separately so they are not mistaken for the source of truth.

## Product and operations

| Document | Audience | Purpose |
|---|---|---|
| [User guide](en/USER_GUIDE.md) | Visitors, patients, support | Booking, accounts, dashboard, reviews, notifications, Denta |
| [Administrator guide](en/ADMIN_GUIDE.md) | Clinic staff, super-admins | Appointments, schedules, content, moderation, analytics, access management |
| [Deployment guide](en/DEPLOYMENT.md) | Maintainers, DevOps | Vercel/Docker setup, migrations, cron, verification, rollback considerations |
| [QA checklist](QA_CHECKLIST.md) | QA, reviewers | Release checks across security, responsive UI, localization, and accessibility |

## Engineering reference

| Document | Purpose |
|---|---|
| [Architecture](en/ARCHITECTURE.md) | System context, component boundaries, data/request flows, major decisions |
| [REST API](en/API.md) | Routes, authorization, session behavior, limits, response semantics |
| [Data dictionary](en/DATA_DICTIONARY.md) | Persistent entities, constraints, indexes, lifecycle rules |
| [Developer guide](en/DEVELOPER_GUIDE.md) | Local setup, configuration, migrations, tests, common change paths |
| [Security](en/SECURITY.md) | Threat boundaries, controls, secrets, retention, residual risks |
| [Postman collection](DentalClinic.postman_collection.json) | Starter requests using the application's cookie-based sessions |
| [Smile Meter](SMILE_METER.md) | Asset-selection and interaction contract for the cosmetic visualizer |

## Architecture decisions and project records

These files preserve engineering context. They are not substitutes for the current reference guides above.

| Record | Status |
|---|---|
| [Denta structured streaming decision](DENTA_STREAMING_DECISION.md) | Accepted architecture decision |
| [Audit progress](AUDIT_PROGRESS.md) | Historical hardening checkpoint |
| [Phase 2 progress](PHASE2_PROGRESS.md) | Historical planning snapshot |
| [Security hardening notes](SECURITY_HARDENING_NOTES.md) | Superseded working notes |
| [Dashboard UI final](DASHBOARD_UI_FINAL.md) | Historical UI change summary |

## Language policy

English is canonical for the GitHub landing page and engineering reference. Russian translations are available at `README.ru.md` and in the corresponding files directly under `docs/`.

When product behavior changes, update the English document and its Russian counterpart in the same pull request. Commands, route names, configuration keys, and code identifiers should remain unchanged in translation.

## Keeping documentation trustworthy

- Describe behavior enforced by the current code, configuration, migrations, or CI.
- Label plans and historical snapshots explicitly.
- Never include production credentials, real patient data, private clinic records, or unredacted logs/screenshots.
- Validate internal links and JSON examples before merging.
- Follow [CONTRIBUTING.md](../CONTRIBUTING.md) for review and verification expectations.
