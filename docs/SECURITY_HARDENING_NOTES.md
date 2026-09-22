# Security Hardening Notes

> **Superseded working notes.** These items were part of an earlier review and may already be implemented. The current source of truth is the [security guide](en/SECURITY.md), with planned work in the [roadmap](../ROADMAP.md).

## Patient dashboard review

- Replace duplicated password validation rules with a shared password policy.
- Keep frontend validation aligned with backend validation.
- Limit external translation payload sizes before sending user-generated text.
- Continue reviewing authentication, localization, and patient data flows for consistency.

## Next checks

- Password reset and change-password flows.
- Translation API input limits.
- Error handling consistency.
- Regression tests for patient account flows.
