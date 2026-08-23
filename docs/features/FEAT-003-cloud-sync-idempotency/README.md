# FEAT-003: Cloud Synchronization and Idempotency

## Outcome

As a store operator, I want locally committed sales to synchronize automatically after connectivity returns without duplicates.

## Scope

- Sync pending outbox messages from edge to the cloud API.
- Use Azure Service Bus for reliable delivery and dead-letter handling.
- Enforce idempotency using store, terminal, and local transaction identity.
- Track retry, pending, synced, conflict, and review states.
- Provide reconciliation and sync-health metrics.

## Acceptance criteria

- Checkout does not require cloud availability.
- Retrying the same command creates one cloud sale.
- Temporary failures retry with backoff; unrecoverable failures become reviewable.
- Events include required IDs, store, timestamp, correlation ID, and schema version.
- A store can inspect oldest pending message and last successful sync.

## Dependencies and QA

Depends on FEAT-002 and the cloud API. Test duplicate delivery, timeout, ordering, dead-letter, reconnect, conflict, and partial outage scenarios.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
