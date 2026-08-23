# FEAT-004: API and Event Contracts

## APIs and commands

- Queries: `GET /api/v1/stores/{storeId}/catalog` and `GET /api/v1/stores/{storeId}/inventory` support version/effective-time and pagination.
- Commands: `POST /api/v1/stores/{storeId}/inventory/receipts`, `POST /api/v1/stores/{storeId}/inventory/adjustments`, and catalog update commands require reason and expected version.
- Authentication and authorization: cashier read access; manager adjustment access; owner configuration access, all scoped to tenant/store.
- Idempotency behavior: commands use client command ID plus store ID; duplicate movement commands return the original movement/result.
- Error model: stable invalid-input, forbidden, stale-version, negative-stock, not-found, and transient codes.

## Events

- Publishes `CatalogProductChanged.v1`, `InventoryMovementRecorded.v1`, `InventoryConflictDetected.v1`, and `LowStockDetected.v1` when applicable.
- Producer: catalog/inventory domain services; consumers: edge sync, checkout read models, reporting, notifications, and audit.
- Required metadata: event ID, aggregate ID, store ID, occurred time, correlation ID, and schema version; inventory events also carry movement ID and aggregate version.
- Delivery and ordering: durable event bus; movement order is preserved per product/store where possible, with version checks for late events.
- Duplicate handling: deduplicate event ID and movement ID; never apply the same movement twice.

## Compatibility

- Additive-change policy: optional catalog fields and new event versions remain readable by older edge clients.
- Breaking-change policy: introduce a new API/event version and support both during store upgrade and pending-message drain.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; persistence and conflict tests in `tests/Integration/RetailPulse.IntegrationTests`.
- Ownership: Catalog/inventory owns product and movement semantics; sync owns transport and retry.
