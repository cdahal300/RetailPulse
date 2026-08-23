# FEAT-010: Rollout and Operations

## Release

- Feature flag: `analytics.reporting.v1` gates new reports/read models; source event capture continues independently.
- Safe default: show last known report with freshness/partial state; do not block checkout or sync on analytics.
- Migration strategy: provision ADLS partitions, event ledger, fact tables, and read models additively; backfill and reconcile before switching queries.
- Deployment order: schemas/ledger, ingestion, transformations, aggregates, query API, PWA panels, then tenant/store cohorts.
- Approval gates: Analytics/data owner, privacy/security, finance, QA, operations, and business reporting owner.

## Rollout

- Targeting plan: internal datasets, staging, one pilot tenant/store cohort, then broader tenants after reconciliation.
- Metrics: freshness, event lag, duplicate/quarantine rate, reconciliation variance, query latency, export volume, ADLS cost, and pipeline failures.
- Alerts and runbooks: alert on freshness breach, backlog, data-quality issue, partition failure, access denial spike, and cost anomaly; link data recovery/replay runbook.
- Expansion criteria: reconciled totals, isolation tests, successful replay/correction, acceptable cost/latency, and governed retention.

## Rollback

- First action: route reports to the last known-good read model or disable new panels; preserve event ingestion.
- Data and event handling: retain raw/versioned inputs and audit corrections; do not delete facts to hide discrepancies.
- Deployment rollback: restore compatible query/read-model version; use forward-compatible reprocessing for schema changes.
- Recovery validation: reconcile source events to facts/aggregates and verify freshness, permissions, exports, and FEAT-013 inputs.

## Ownership

- Feature owner: Analytics and data platform team.
- On-call owner: Data pipeline operations.
- Expiry or cleanup issue: retire legacy aggregates, dual-read paths, and migration flags after reconciliation sign-off.
