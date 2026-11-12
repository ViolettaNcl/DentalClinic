# Changelog

All notable changes to DentalClinic are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); entries are grouped by release date because
the project ships continuously from `main`. Russian summary is provided for each release.

## 2026-11-12 — Documentation experience

### Added
- Animated README hero, product showcase (real screens of the current build with a phone preview),
  architecture diagram with flowing data packets, section dividers and footer — pure SVG + CSS/SMIL,
  no external services, `prefers-reduced-motion` respected, readable without animation.
- Fresh screenshots in `docs/screenshots/*.webp` (home, services, doctors, contact route, mobile).
- This changelog.

### Changed
- README (EN/RU) restructured: feature cards, "What's new", stack badges, quality gates, collapsible
  setup/configuration, bilingual documentation index.
- API, architecture, security, developer, deployment, user and QA guides describe the current session,
  map, motion and caching behaviour.

> **RU:** Новая анимированная витрина README (баннер, экраны продукта, схема архитектуры), свежие
> скриншоты, переработанные README и руководства под текущее поведение системы.

## 2026-11-11 — Sign-in reliability, routes and motion

### Fixed
- **Patient sign-in across instances.** Logout bumped the account token version only in one instance's
  memory, so another Vercel instance rejected the next fresh login/registration and the patient was sent
  back to the home page. Logout now revokes only the current token id (`jti`).
- **Silent logouts on slow databases.** Token-version lookups that time out or hit a transient SQL error
  in production return a retryable `503` instead of `401`; the browser retries session bootstrap twice.
- **Lost early clicks in the header.** The language switcher and burger menu are wired before i18n and
  the session check; this also removes the race that failed the Production E2E language test.
- Header no longer blocks on the session check for up to 25 s; expired sessions restore the
  "Sign in / Sign up" buttons.
- Duplicate hover underline in the navigation; page-level horizontal scroll on phones; fixed-position
  mobile buttons displaced by an animated header.

### Added
- **In-page routes** on the contact page via the Yandex Maps widget (driving, walking, transit) and an
  "open in Yandex Maps / Navigator" link; the legacy Google embed was no longer framable.
- **Motion layer** (`motion.css`, `siteMotion.js`) on all public pages: reveals, hero particles, cursor
  glow and parallax, 3D card tilt with spotlight, ripple and shine, magnetic CTAs, counters, modal and
  menu animations, curtain reveal for photos, smooth page transitions.
- Symmetric desktop header grid (logo · centred menu · actions).
- `Cache-Control: no-cache` for HTML, JS, CSS and JSON so browsers revalidate via `ETag` after deploys.

> **RU:** Исправлен вход и регистрация пациента на нескольких экземплярах, медленная БД больше не
> разлогинивает, маршрут на карте прямо на странице (Яндекс.Карты), анимации на всём сайте,
> симметричная шапка, адаптив без горизонтальной прокрутки, исправлен E2E-тест переключателя языка.

## 2026-09-22 — Community files

- Contribution guidelines, public roadmap, issue templates and an animated README experience.

## 2026-09-17 — Responsive UI and Smile Meter

- Final responsive UI pass, Smile Meter and dashboard improvements; Gemini key-boundary CI fixes.

## 2026-09-07 … 2026-09-14 — Phase 0/1 hardening

- Shared strong-password policy, cross-role email uniqueness, database domain constraints.
- Same-origin protection for cookie API writes, Permissions-Policy header, safe link rendering.
- SQL-backed distributed rate limits for auth and appointments; super-admin access management.
- Managed Denta knowledge base with query ranking; configurable public clinic profile.
- Notification idempotency, bounded exports, resilient registration and status updates.
- Vercel container service, registry retention automation and migration fixes.

For the complete list see the [commit history](https://github.com/ViolettaNcl/DentalClinic/commits/main).
