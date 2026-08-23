# FEAT-005: Identity and Role Authorization

## Outcome

As a store owner, I want each user and device to have controlled access so that sales, settings, and manager actions are protected.

## Scope

- Entra External ID integration for cloud users.
- Cashier, manager, owner, and device roles.
- Store and tenant isolation.
- Device registration and controlled offline session behavior.
- Audit events for privileged actions.

## Acceptance criteria

- Protected API routes enforce role and store authorization server-side.
- A cashier cannot perform manager-only actions.
- Expired, revoked, or invalid tokens are rejected.
- Offline edge sessions use bounded cached authorization and safe expiry behavior.
- Logs contain identity and store context without secrets or card data.

## Dependencies and QA

Required before production PWA and manager commands. Test token expiry, revocation, role changes, cross-store access, offline expiry, and unauthorized API access.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [Tenant isolation checklist](../../planning/tenant-isolation-checklist.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)

## Implementation Status

- Completed: server-side protected cloud and edge routes for role and scope checks.
- Completed: cashier, manager, owner, and device role matrix enforcement.
- Completed: bounded offline edge session cache with revocation-aware invalidation.
- Completed: idempotent device registration and revocation commands.
- Completed: user role change command with revocation-based session refresh behavior.
- Completed: audit events for privileged actions and token rejections.
- Completed: event contracts for UserRoleChanged.v1, DeviceRegistered.v1, DeviceRevoked.v1, and PrivilegedActionAudited.v1.

## Verification Evidence

- Unit tests: IdentityAuthorization and IdentityLifecycle test classes pass.
- Integration tests: IdentityAuthorizationEndpointIntegrationTests pass, including 401/403/200 matrix, cross-store denial, role changes, and revocation.
- Contract tests: IdentityAuthorizationContractTests pass for required event metadata and schema versioning.
