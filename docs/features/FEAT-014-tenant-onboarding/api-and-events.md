# FEAT-014: API and Event Contracts

## APIs and commands

- `POST /api/v1/onboarding/tenants` creates an internal tenant and first owner membership. Requires an idempotency key.
- `GET /api/v1/me` returns the authenticated subject, internal tenant memberships, roles, and permitted store IDs.
- `GET /api/v1/tenants/{tenantId}/stores` returns only stores permitted to the caller.
- `POST /api/v1/tenants/{tenantId}/stores` creates a store for an authorized owner.
- `GET /api/v1/tenants/{tenantId}/onboarding` returns completed and pending onboarding steps for an authorized owner.
- `POST /api/v1/tenants/{tenantId}/invitations` creates an owner or manager invitation with explicit store assignments and an expiry.
- `POST /api/v1/tenants/{tenantId}/invitations/{invitationId}/accept` accepts a valid invitation idempotently.
- `POST /api/v1/tenants/{tenantId}/stores/{storeId}/users/{subjectId}/roles` assigns or changes a role only within the caller's tenant scope.
- Existing device registration routes remain store-scoped and must reject a device already bound to another tenant or store.

## Scope rules

- `RetailPulseTenantId` is an internal business identifier and is never taken from an untrusted client as authority.
- External identity claims retain issuer, external identity tenant, subject/object ID, and audience information.
- Owner membership with no store restriction resolves to all current stores in that internal tenant.
- Manager membership contains an explicit permitted store set.
- Device identity contains exactly one tenant and store and only the device role.
- Every repository, cache key, event, command, analytics query, export, and AI input requires tenant scope; store scope is required for store-owned data.

## Events

- `TenantCreated.v1`: tenant ID, owner subject, occurred time, correlation ID, actor, and schema version.
- `StoreCreated.v1`: tenant ID, store ID, configuration version, actor, occurred time, correlation ID, and schema version.
- `InvitationCreated.v1`: tenant ID, invitation ID, role, store assignments, expiry, actor, occurred time, and schema version.
- `MembershipChanged.v1`: tenant ID, subject ID, role, permitted store IDs, membership version, actor, occurred time, and schema version.
- `InvitationAccepted.v1`: tenant ID, invitation ID, subject ID, membership ID, occurred time, correlation ID, and schema version.

Consumers must revalidate tenant/store scope and deduplicate event IDs. Membership changes invalidate affected PWA sessions and bounded edge authorization caches.

## Compatibility

- Additive response fields are preferred; clients must tolerate unknown onboarding status fields.
- Breaking changes require versioned commands or event schemas and an overlap window.
- Contract tests belong in `tests/Contract/RetailPulse.ContractTests`; authorization and persistence tests belong in the integration suite; owner onboarding flows belong in `tests/Pwa`.
