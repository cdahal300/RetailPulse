# FEAT-002: Rollout and Operations

## Release

- Feature flag: `checkout.offline.v1` controls the new durable edge path.
- Safe default: enabled only where the existing edge path and migration checks pass; preserve the existing safe fallback and never claim an uncommitted sale.
- Migration strategy: version schema migrations, take a verified backup where supported, run additive migrations before activation, and support old readers during the transition.
- Deployment order: integration environment, test edge image, staging pilot, one internal store, then progressive store rollout.
- Approval gates: Edge owner, QA, operations, and release owner approve migration evidence and recovery rehearsal.

## Rollout

- Targeting plan: environment then pilot store; expand only after restart, offline, reconnect, and storage-failure checks pass.
- Metrics: commit success/failure, transaction latency, SQLite lock/capacity errors, schema version, recovery count, pending outbox count, and checkout fallback rate.
- Alerts and runbooks: alert on failed commits, corruption, capacity threshold, migration failure, and rising fallback; link the edge persistence recovery runbook.
- Expansion criteria: no data-loss or duplicate findings, healthy restart recovery, bounded latency, and acknowledged storage capacity headroom.

## Rollback

- First action: disable `checkout.offline.v1` for affected stores while preserving local data for recovery.
- Data and event handling: do not delete databases or outbox records; quarantine uncertain transactions and reconcile before reactivation.
- Deployment rollback: revert the edge image only if the schema remains readable by the prior version; otherwise restore the compatible image and complete a forward migration.
- Recovery validation: verify sale counts, payment references, inventory movements, receipt intents, and outbox state against the recovery report.

## Ownership

- Feature owner: Edge platform team.
- On-call owner: Store edge operations.
- Expiry or cleanup issue: remove the flag after all stores use durable persistence and migration compatibility is retired.
