# FEAT-010: Analytics and Reporting

## Status

Event-backed dev slice complete; production analytics hardening remains. The Cloud API now supports versioned sale-event ingestion, source-event deduplication, tenant/store-scoped PostgreSQL facts, event-backed hourly sales aggregation, and a dev-only seed path. ADLS ingestion, replay/correction workflows, and production-scale quality controls remain pending.

## Outcome

As an owner or manager, I want trustworthy sales and inventory reports so that I can understand performance and act on trends.

## Scope

- Consume versioned business events without querying checkout tables directly.
- Build curated sales, inventory, refund, and sync facts in ADLS Gen2.
- Create daily/hourly aggregates and read models for dashboard queries.
- Provide data freshness, source links, and tenant/store filtering.
- Retention, partitioning, and correction/reprocessing procedures.

## MVP Simulation Slice

- `GET /api/v1/tenants/{tenantId}/stores/{storeId}/reports/sales` returns a sales summary, hourly buckets, top products, freshness metadata, and `DataSource = simulated-events`.
- Report reads require manager or owner authorization and enforce tenant/store scope server-side.
- Simulated facts are deterministic, duplicate-aware, tenant/store filtered, and exclude raw payment/card data.
- `scripts/generate-analytics-traffic.sh` can repeatedly call the report endpoint against a local port-forward or deployed API base URL.
- Real ingestion from versioned events remains the next analytics hardening step and should plug into the `IAnalyticsReportProvider` boundary.

## Verified Event-Backed Dev Slice

- `SaleCompletedEvent.v1` maps to idempotent analytics sales facts.
- PostgreSQL migration v4 creates the facts table, scope index, and tenant/store RLS policy.
- Dev `Analytics__UseEventFacts=true` selects the event-backed provider.
- Authenticated development seed requests produced `event-facts` reports with complete freshness metadata in the PWA.
- Production ADLS/stream ingestion, late-event correction, and replay are still pending.

## Acceptance criteria

- Dashboard totals reconcile to source sales within the documented timing window.
- Duplicate events do not double-count revenue or inventory.
- Reports show freshness and handle delayed synchronization.
- Store and tenant isolation is enforced in queries.
- Raw payment data is excluded from analytics storage.

## Dependencies and QA

Depends on FEAT-003 and FEAT-008. Test late events, duplicates, corrections, timezone boundaries, currency handling, retention, reconciliation, and query performance.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
