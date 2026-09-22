# DentalClinic QA checklist

[Documentation](README.md) · [Deployment guide](en/DEPLOYMENT.md) · [Security guide](en/SECURITY.md)

Use this checklist for a release candidate or a substantial user-facing change. Record the tested commit, environment, browser/device matrix, and any accepted exceptions in the pull request or release record.

## Authentication and database

- Verify existing patient and administrator accounts can sign in against the configured SQL Server database.
- Verify new patient registration persists and can sign in afterwards.
- Verify logout clears the browser session cleanly.
- Do not replace the configured production/shared database with local demo data for release testing.

## Public site and services

- Verify Home, About, Services, Doctors, Contact and every service-detail page at mobile, tablet and desktop widths.
- Confirm service pricing cards keep headings, price panels and CTA buttons aligned within each row.
- Confirm price amounts do not wrap unexpectedly.
- Confirm no translated service title displays raw HTML such as `<br>`.
- Verify the mobile header keeps the hamburger at the right edge and the language control directly to its left.

## Dashboards

- Verify Admin and Patient dashboards on desktop, tablet and mobile.
- Confirm mobile navigation opens as a drawer rather than leaving a permanent desktop sidebar.
- Confirm tables scroll inside their own containers instead of making the whole page horizontally scroll.
- Verify dashboard toasts remain visible near the top-right and do not cover primary actions.

## Smile Meter

- Verify Front / Upper / Lower / Side views use the correct camera family.
- Verify whitening, alignment and shape stages map to visually appropriate assets.
- Confirm only one dental model image is visible at runtime.
- Check rapid slider movement for stale transitions, flicker or ghosting.
- Check Natural / Balanced / Hollywood presets and reduced-motion mode.

## Localization and accessibility

- Verify RU, EN, FR, EL and AR on all major pages.
- Verify Arabic `dir=rtl` layout for header, cards, forms, dashboards, Smile Meter, chat and modals.
- Check visible keyboard focus, labels, modal keyboard navigation and practical touch targets.

## Before release

- Run `dotnet build DentalClinic.csproj --configuration Release`.
- Run `dotnet test DentalClinic.Tests/DentalClinic.Tests.csproj --configuration Release`.
- Run `npm run test:js` and the relevant Playwright projects.
- Run responsive browser tests and accessibility checks where the environment supports them.
- Review database migrations before deployment.
- Confirm production secrets are supplied through secure configuration rather than committed documentation or source.
- Confirm `vercel.json` deployment policy matches the intended release process; Git-triggered deployment is currently paused.
- Verify `/health`, expected security headers, same-origin rejection, cron-secret rejection, and database backup/restore readiness.
