# FEAT-005: Rollout and Operations

## Release

- Feature flag: `identity.authorization.v1` gates enforcement only for prepared routes; authorization cannot be disabled for existing protected operations.
- Safe default: deny unknown or insufficient claims; preserve only explicitly supported bounded offline sessions.
- Migration strategy: provision Entra applications, role/permission mappings, device records, and audit schema additively; dual-read claims during transition.
- Deployment order: identity config and keys, cloud middleware, audit/events, edge session handling, PWA routes, then store cohorts.
- Approval gates: Security, identity owner, privacy/compliance, QA, operations, and business owner approve the role matrix.

## Rollout

- Targeting plan: test tenant, internal users/devices, pilot store, then tenant/store cohorts.
- Metrics: authentication failures, forbidden rates by route/role, revocation latency, cache age, device registration failures, and audit delivery.
- Alerts and runbooks: alert on token validation failures, cross-scope attempts, revocation lag, and identity-provider outage; link identity incident runbook.
- Expansion criteria: zero cross-store access findings, complete privileged audit records, and successful offline expiry/revocation tests.

## Rollback

- First action: stop new role/device onboarding and restrict new routes to deny-by-default policy.
- Data and event handling: preserve audit and revocation records; do not roll back security events or widen access to recover availability.
- Deployment rollback: revert application binaries only with compatible role mappings and token validation; restore configuration through approved change.
- Recovery validation: replay role matrix, cross-scope denial, revocation, and audit completeness checks.

## Ownership

- Feature owner: Identity and security team.
- On-call owner: Platform identity operations.
- Expiry or cleanup issue: retire transitional claims/cache flag after all clients enforce the versioned policy.
