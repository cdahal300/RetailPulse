# FEAT-005: API and Event Contracts

## APIs and commands

- Identity integration: OIDC discovery/JWKS validation with Entra External ID; no RetailPulse public credential exchange is introduced.
- APIs: `GET /api/v1/me`, `POST /api/v1/tenants/{tenantId}/stores/{storeId}/devices/register`, `POST /api/v1/tenants/{tenantId}/stores/{storeId}/devices/{deviceId}/revoke`, `POST /api/v1/tenants/{tenantId}/stores/{storeId}/users/{subjectId}/roles`, and policy-protected store/manager command endpoints.
- Authentication and authorization: bearer tokens for cloud APIs; device-bound credentials and bounded cached claims for supported edge operations; server-side tenant/store/role checks.
- Edge revocation propagation: `POST /api/v1/edge/tenants/{tenantId}/stores/{storeId}/identity/revoke-subject/{subjectId}` invalidates bounded cached sessions when revocation evidence arrives.
- Idempotency behavior: device registration, revocation, and privileged commands use command IDs and return the original result on retry.
- Error model: stable unauthenticated, forbidden, invalid-token, revoked-device, scope, and transient identity-provider errors without leaking policy details.

## Events

- Publishes `UserRoleChanged.v1`, `DeviceRegistered.v1`, `DeviceRevoked.v1`, and `PrivilegedActionAudited.v1` when applicable.
- Producer: identity/authorization module; consumers: edge session cache, audit, notifications, and operational monitoring.
- Required metadata: event ID, aggregate ID, store ID where applicable, occurred time, correlation ID, and schema version.
- Delivery and ordering: durable delivery; role/device changes are ordered per subject or device where required.
- Duplicate handling: consumers deduplicate event ID and apply monotonic subject/device version.

## Compatibility

- Additive-change policy: add claims/permissions only with safe defaults and preserve existing role semantics.
- Breaking-change policy: version policy and event schemas; overlap old/new claims for the cache window before enforcement.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; authorization integration tests in `tests/Integration/RetailPulse.IntegrationTests`.
- Ownership: Identity owns authentication and authorization policy; each feature owns business permission definitions.
