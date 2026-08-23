# FEAT-003: API and Event Contracts

## APIs and commands

- Cloud command: `POST /api/v1/sync/sales` accepts store ID, terminal ID, local transaction ID, event envelope, payload, and idempotency key; it returns accepted, duplicate, rejected, or reviewable status.
- Query: `GET /api/v1/stores/{storeId}/sync-health` returns pending age, last success, retry, conflict, and dead-letter summaries.
- Authentication and authorization: device credentials are bound to store and tenant; server-side authorization rejects mismatches.
- Idempotency behavior: the key is store ID + terminal ID + local transaction ID; durable deduplication returns the original result for retries.
- Error model: stable validation, unauthorized, conflict, transient, and reviewable codes; clients may retry only documented transient responses.

## Events

- Consumes edge outbox messages and publishes `SaleCompleted.v1`, `InventoryMovementRecorded.v1`, and `SyncStatusChanged.v1` when applicable.
- Producer: cloud sync command and reconciliation worker; consumers: inventory, read models, analytics, and operational tooling.
- Required metadata: event ID, aggregate ID, store ID, occurred time, correlation ID, and schema version.
- Delivery and ordering: Azure Service Bus provides durable delivery; ordering is scoped by store/aggregate where required and consumers tolerate late delivery.
- Duplicate handling: consumers deduplicate by event ID and aggregate version; redelivery is expected.

## Compatibility

- Additive-change policy: add optional fields and preserve v1 envelopes and response codes.
- Breaking-change policy: introduce a new version and dual-read/dual-publish transition; never invalidate pending edge messages.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; sync persistence tests belong in `tests/Integration/RetailPulse.IntegrationTests`.
- Ownership: Sync owns ingestion, deduplication, retry, and reconciliation; domain modules own event meaning.
