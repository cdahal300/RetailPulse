# FEAT-005: Acceptance Criteria

## Functional behavior

- Given a valid Entra External ID user or registered device token, when a protected route is called, then identity, tenant, store, and roles are resolved server-side.
- Given cashier, manager, owner, or device role, when an allowed operation is requested, then the operation succeeds only within its assigned scope.
- Given a privileged action, when it completes, then an audit event includes actor, target, store, reason, and correlation ID.

## Failure and resilience behavior

- Given an expired, revoked, malformed, or unavailable identity token, when access is attempted, then the request is denied safely and no partial mutation occurs.
- Given an offline edge session, when its bounded authorization cache is valid, then supported operations continue; after expiry or revocation evidence, sensitive operations stop safely.
- Given identity-provider outage, then local cart and checkout recovery behavior is not blocked, while cloud privileged commands fail closed.

## Authorization and isolation

- Enforce authorization on every cloud route and edge command, including tenant and store scope, not only in the UI.
- Cashiers cannot perform manager adjustments; managers cannot access another store; owners cannot access another tenant; devices cannot impersonate users.
- Role changes and device revocation take effect within the documented cache/revocation window and are auditable.

## Data and security

- Sensitive data handling: store only required subject IDs, role/store claims, token metadata, and audit references; never persist tokens unnecessarily or any card data.
- Audit requirements: record sign-in, token rejection, role/device changes, privileged commands, policy decisions, and administrative access.
- Retention and deletion: follow tenant privacy retention for identity/audit data while preserving security and financial audit obligations.
- Use OIDC validation, issuer/audience/signature checks, least privilege, secure device registration, and secret redaction.
