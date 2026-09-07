# Security Hardening Notes

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
