# FEAT-003: Rollout and Operations

## Release

- Feature flag: `sync.cloud.v1` gates cloud submission; local checkout remains independent.
- Safe default: sync disabled or observe-only for unregistered stores; the edge continues durable outbox capture.
- Migration strategy: deploy additive deduplication/state tables and indexes before workers; retain pending v1 messages.
- Deployment order: Service Bus and cloud schema, API, worker, edge sync agent, staging, pilot store, then expansion.
- Approval gates: Cloud owner, edge owner, security, QA, and operations approve duplicate/replay and dead-letter evidence.

## Rollout

- Targeting plan: development, staging, internal store, one production store, then store cohorts by region.
- Metrics: accepted/duplicate/rejected counts, sync age, retry rate, backlog, dead letters, conflicts, and per-store availability.
- Alerts and runbooks: page on oldest pending age, dead-letter spike, duplicate anomaly, and Service Bus throttling; link sync recovery runbook.
- Expansion criteria: no duplicate effects, bounded backlog after reconnect, and successful replay rehearsal.

## Rollback

- First action: disable cloud submission while leaving local outbox capture enabled.
- Data and event handling: preserve pending and dead-letter records; stop replay until the compatible consumer is restored.
- Deployment rollback: roll back workers/API only after confirming they can read current envelopes and deduplication state.
- Recovery validation: reconcile edge, cloud, and event counts by store and local transaction ID.

## Ownership

- Feature owner: Edge/cloud synchronization team.
- On-call owner: Cloud platform operations.
- Expiry or cleanup issue: retire the flag after all stores use the stable sync protocol and replay tooling is accepted.
