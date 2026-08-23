# FEAT-010: Acceptance Criteria

## Functional behavior

- Given versioned sales, inventory, refund, and sync events, when ingestion completes, then curated facts and hourly/daily aggregates are available with source references and freshness.
- Given an authorized owner or manager query, when a report runs, then totals are filtered by tenant/store and timezone/currency rules and reconcile within the documented timing window.
- Given a late, corrected, or reprocessed event, when the pipeline reruns, then facts and aggregates converge deterministically without double counting.

## Failure and resilience behavior

- Given delayed sync, malformed event, duplicate delivery, or pipeline outage, then data is marked delayed/partial, quarantined or retried, and never presented as fresh and complete.
- Given ADLS, stream processor, or read-model unavailability, then checkout and sync remain independent and operators can recover/replay from durable inputs.
- Given a timezone boundary, currency mismatch, or correction, then the report applies documented policy and exposes the affected source/freshness state.

## Authorization and isolation

- Queries enforce tenant/store scope server-side and prevent raw cross-tenant joins or exports.
- Owners see authorized tenant stores; managers see assigned stores; operational and raw datasets are limited to approved roles.

## Data and security

- Sensitive data handling: analytics excludes PAN, CVV, PIN, magnetic-stripe, raw card data, and unnecessary PII; use opaque payment references only when needed for reconciliation.
- Audit requirements: record query/export actor, source event versions, corrections, reprocessing, access policy, and report definition version.
- Retention and deletion: partition and retain raw/curated facts per policy; support tenant deletion/correction workflows without breaking financial audit requirements.
- Encrypt ADLS/PostgreSQL in transit/at rest, restrict exports, validate schemas, and apply row-level access and managed identity.
