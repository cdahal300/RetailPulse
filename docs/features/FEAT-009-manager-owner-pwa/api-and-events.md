# FEAT-009: API and Event Contracts

## APIs and commands

- Queries: `GET /api/v1/me`, store-scoped sales/inventory/sync-health/insight endpoints, and notification preferences.
- Commands: authorized manager commands use `POST /api/v1/stores/{storeId}/commands` with command type, payload, client command ID, and expected version.
- Authentication and authorization: Entra-backed session and server-side role/store/tenant policy; PWA never trusts route visibility for authorization.
- Idempotency behavior: client command ID plus store/user scope deduplicates retries; response includes pending, accepted, confirmed, or reviewable status.
- Error model: stable unauthenticated, forbidden, stale, validation, offline, transient, and reviewable states safe for user display.

## Events

- No new public domain event is required for the PWA. It consumes existing read models and notifications; command side effects publish events owned by their domain.
- Notification delivery may use `LowStockDetected.v1`, `SyncStatusChanged.v1`, and approved operational events.
- Ownership: PWA owns presentation/cache contracts; cloud domains own authorization, command, and event semantics.

## Compatibility

- Additive-change policy: optional response fields and cache records; clients tolerate unknown fields and stale data.
- Breaking-change policy: version API/service-worker/cache schemas and support the previous client during deployment propagation.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; PWA E2E/accessibility suites under `tests/Pwa`.
