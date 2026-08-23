# FEAT-004: Rollout and Operations

## Release

- Feature flag: `catalog.inventory.v1` gates new reads and manager commands; checkout retains the last safe local catalog.
- Safe default: read-only cached catalog and no destructive adjustment when service or authorization is unavailable.
- Migration strategy: create ledger/read-model schema additively, backfill from approved sources, and retain movement history.
- Deployment order: cloud schema/read models, event consumers, edge cache refresh, PWA commands, then store cohorts.
- Approval gates: Inventory owner, finance/operations, security, QA, and store pilot manager.

## Rollout

- Targeting plan: internal stores, one pilot per operating region, then stores with verified catalog quality.
- Metrics: lookup latency, catalog freshness, movement conflicts, negative-stock rejects, reconciliation variance, and low-stock alert accuracy.
- Alerts and runbooks: alert on refresh age, balance variance, conflict backlog, and event consumer lag; link inventory reconciliation runbook.
- Expansion criteria: reconciled opening balances, successful offline lookup, authorized adjustment evidence, and no silent sale overwrite.

## Rollback

- First action: disable manager mutations and new catalog writes; retain read-only last-known-good data.
- Data and event handling: preserve ledger and queued movements; replay only with the compatible version and conflict review.
- Deployment rollback: revert UI/API consumers only after schema readers remain compatible; use forward migration for irreversible ledger changes.
- Recovery validation: compare movement ledger, materialized balance, edge cache version, and sales-derived stock by store.

## Ownership

- Feature owner: Catalog and inventory team.
- On-call owner: Store operations with cloud data on-call.
- Expiry or cleanup issue: remove the flag after catalog refresh, movement reconciliation, and conflict tooling are standard.
