# DentalClinic roadmap

This roadmap communicates direction, not a delivery commitment. Priorities may change based on clinic needs, security findings, provider constraints, and contributor capacity.

[README](README.md) · [Documentation](docs/README.md) · [Contributing](CONTRIBUTING.md)

## Shipped foundations

- Public multilingual clinic website with responsive service and doctor pages
- Guest and registered-patient appointment workflows
- Patient and administrator dashboards
- Doctor schedules, collision checks, configurable clinic hours, and reporting
- Review moderation and realtime patient notifications
- Multiple administrator accounts with super-admin safeguards
- Database-backed services, doctor profiles, and Denta knowledge management
- Structured Gemini assistant with bounded clinic links and healthcare-safety rules
- Durable avatar/doctor image storage, reminders, follow-ups, cleanup, and retention jobs
- CI, CodeQL, Docker builds, production smoke tests, and Playwright accessibility coverage

## Near-term priorities

- Multilingual authoring/translation workflow for administrator-managed clinic knowledge
- Password recovery and stronger account-security options such as MFA for administrators
- Expanded analytics with clearly defined operational KPIs
- Improved notification preferences and delivery channels
- Explicit production backup/restore runbooks and recovery exercises
- Broader accessibility review across authenticated dashboards and complex widgets

## Candidate product work

- Dental chart / odontogram workflows
- Payment and invoice integrations
- Patient communication templates and appointment wait-list workflows
- Mobile/PWA experience after the web workflows and offline requirements are validated
- Additional AI-assisted clinic workflows only where deterministic validation and human review are available

## Technical investments

- Evaluate asymmetric token signing or a managed identity provider as the deployment footprint grows
- Add observability for latency, provider failures, quota pressure, and background-job outcomes without storing sensitive content
- Continue reducing mutable infrastructure assumptions and documenting controlled release procedures
- Add performance budgets and repeatable load tests for public catalogue and booking paths

Suggestions are welcome through [GitHub Issues](https://github.com/ViolettaNcl/DentalClinic/issues). Security reports must follow [SECURITY.md](SECURITY.md), not a public issue.
