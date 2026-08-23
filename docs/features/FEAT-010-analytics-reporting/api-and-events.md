# FEAT-010: API and Event Contracts

## APIs and commands

- MVP simulated query: `GET /api/v1/tenants/{tenantId}/stores/{storeId}/reports/sales` accepts `from`, `to`, `timezone`, and `currency` query parameters and returns sales summary, hourly sales, top products, freshness, and report schema version.
- Planned queries: `GET /api/v1/reports/sales`, `/inventory`, `/refunds`, and `/sync-health` accept authorized store scope, time range, timezone, currency, freshness, and pagination.
- Commands: `POST /api/v1/analytics/reprocess` and correction commands require owner/operator authorization, reason, scope, and idempotency key.
- Authentication and authorization: server-side tenant/store row-level policy; exports require explicit permission and audit.
- Idempotency behavior: event ingestion deduplicates event ID plus source/version; reprocessing uses job ID and deterministic partition replacement.
- Error model: stable invalid-range, forbidden, stale/partial, unsupported-currency, schema, throttled, and unavailable codes.

The MVP simulated provider deduplicates by source event ID and reports duplicate counts in freshness metadata. It does not persist facts or consume Service Bus/ADLS yet.

## Events

- Consumes `SaleCompleted.v1`, `InventoryMovementRecorded.v1`, `RefundCompleted.v1`, and `SyncStatusChanged.v1`.
- May publish `AnalyticsFactCorrected.v1`, `ReportRefreshCompleted.v1`, and `DataQualityIssueDetected.v1` for downstream operations/insights.
- Producer: domain event producers and analytics pipeline; consumers: reporting, notifications, and FEAT-013.
- Required metadata: event ID, aggregate ID, store ID, occurred time, correlation ID, and schema version; fact records add source event ID and processing version.
- Delivery and ordering: durable ingestion with late-event windows, watermark/freshness tracking, and partition ordering where required.
- Duplicate handling: idempotent event ledger and upsert/replace keyed by source event ID and fact grain.

## Compatibility

- Additive-change policy: optional event fields, new report columns, and new aggregate versions without changing existing meanings.
- Breaking-change policy: version facts/events/report schemas; dual-read/dual-write and backfill before retiring an old version.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; pipeline/storage tests in `tests/Integration/RetailPulse.IntegrationTests`.
- Ownership: Analytics owns fact/aggregate definitions and quality; domain teams own source event contracts.
