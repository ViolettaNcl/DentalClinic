# DentalClinic Phase 2 Progress

## Phase 2 — IN PROGRESS

### Denta / knowledge base

- [x] Added a database-backed `ClinicKnowledgeItem` domain for administrator-managed clinic facts.
- [x] Added Admin-only CRUD API at `/api/clinic-knowledge`.
- [x] Added SQL migration and EF model/index/check-constraint coverage.
- [x] Connected active knowledge items to Denta's server-authoritative prompt block.
- [x] Added prompt-size bounding through `ChatKnowledge:MaxItems` (default 12, hard maximum 30).
- [x] Sanitized managed rows before prompt insertion and explicitly treated them as untrusted data subordinate to clinical-safety policy.
- [x] Added an administrator UI in the existing dashboard for search, create, edit, activate, and deactivate knowledge items.
- [x] Added .NET and JavaScript regression coverage for active-only selection, configured limits, sanitization, persistence metadata, migration shape, Admin-only access, and admin payload validation.
- [x] Added query-aware retrieval/ranking so Denta sends only relevant managed knowledge rows for the current user message, with a hard 200-row candidate ceiling and no arbitrary fallback rows on a miss.
- [ ] Add multilingual knowledge authoring/translation strategy without inventing clinic facts.

### Remaining Phase 2 areas

- [ ] Patient automation and reminders improvements.
- [ ] Analytics dashboard improvements.
- [ ] Branding/domain work.

Deployment/Vercel work remains paused by operator instruction.
