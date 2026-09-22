## Summary

Explain the problem and scope of the change.

## Verification

List checks actually run, their results, and anything not verified. For documentation-only changes, check links, examples, and consistency with code. Do not claim application tests unless you ran them.

## Risk and rollout

Describe schema changes, configuration requirements, compatibility, and rollback where relevant. Documentation-only changes to `main` can still trigger existing delivery workflows.

## Review checklist

- [ ] No secrets, real patient data, or private logs are included.
- [ ] Authorization, ownership, and scheduling rules are preserved where affected.
- [ ] Relevant tests and English/Russian documentation are updated, or exclusions are explained.
- [ ] UI changes consider all five languages, Arabic RTL, and accessibility.
- [ ] Historical records and proposed work are distinguished from current behavior.
