# Security policy

## Supported version

Security fixes are applied to the current `main` branch. Older commits, forks, local modifications, and third-party deployments are not maintained by this repository.

## Reporting a vulnerability

Please do **not** open a public issue, discussion, or pull request containing exploit details, credentials, patient information, or a reproducible attack against a live deployment.

Use GitHub's private **Report a vulnerability** flow on the repository Security tab when it is available. If that option is unavailable, contact the maintainer through the contact method published on [@ViolettaNcl](https://github.com/ViolettaNcl) and share only enough information to establish a private channel.

Include:

- the affected route, component, or commit;
- impact and prerequisites;
- minimal reproduction steps or a proof of concept;
- whether a production system or real data may be affected;
- any suggested mitigation.

Do not test by accessing, modifying, or deleting another person's data; degrading service; sending large automated traffic; or extracting secrets.

## Response expectations

This repository does not publish a guaranteed response time. Coordinate disclosure privately with the maintainer and avoid publishing sensitive details while a report is being assessed.

## Scope notes

The application integrates SQL Server, Google Gemini, ElevenLabs, Vercel, GitHub Actions, and optionally an FTPS host. Vulnerabilities in those services should also be reported to the relevant provider. Exposed credentials should be revoked and rotated immediately rather than waiting for a code change.

For the implemented controls, trust boundaries, and known residual risks, see [`docs/en/SECURITY.md`](docs/en/SECURITY.md).
