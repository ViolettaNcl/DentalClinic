# DentalClinic QA Checklist

## Multilingual interface

- Verify all navigation labels exist in RU, EN, EL, AR and FR.
- Check language switching without page reload.
- Verify right-to-left layout for Arabic pages.
- Confirm chatbot responses keep the selected user language.

## AI consultant

- Verify answers are based on clinic data sources.
- Check fallback behavior when AI services are unavailable.
- Confirm no secrets or API keys are exposed in client-side code.

## Patient workflow

- Registration and login flow.
- Appointment creation, status updates and cancellation.
- Review submission and translation flow.

## Security

- Validate JWT expiration and authorization rules.
- Check rate limiting on public endpoints.
- Confirm production secrets are stored only in environment configuration.

## Before release

- Run automated tests.
- Review database migrations.
- Verify responsive layouts on mobile devices.
