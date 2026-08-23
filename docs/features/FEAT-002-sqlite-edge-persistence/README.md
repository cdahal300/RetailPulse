# FEAT-002: Durable SQLite Edge Persistence

## Outcome

As a store, I want checkout data to survive process restarts and network outages so that committed sales are never lost.

## Scope

- Add SQLite persistence for products, sales, sale lines, payments, inventory movements, receipt intents, outbox messages, and sync state.
- Commit sale, payment reference, inventory movement, receipt intent, and outbox message in one transaction.
- Add schema versioning and startup migration checks.
- Replace the in-memory persistence adapter in the Edge runtime.

## Acceptance criteria

- A committed sale survives process restart.
- A failed transaction leaves no partial sale, inventory movement, or outbox message.
- Database migrations are repeatable and backward-compatible during rollout.
- No raw payment-card data is persisted.
- Local storage capacity failures fail safely and are observable.

## Dependencies and QA

Depends on FEAT-001. Use SQLite integration tests for atomicity, restart recovery, migration, corruption handling, and storage-full behavior. Roll out behind `checkout.offline.v1` with the safest local fallback.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
