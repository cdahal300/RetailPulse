# FEAT-002: API and Event Contracts

## APIs and commands

- Edge command: `CommitSale` accepts store, terminal, local transaction identity, sale lines, totals, payment reference, and correlation ID; it returns committed or rejected status.
- Edge query: `GetPersistenceHealth` returns schema version, database availability, pending outbox count, and last recovery error without exposing sensitive data.
- These are local edge contracts, not new public cloud APIs or externally consumable events.
- Authentication and authorization: local device/session authorization and store binding are required; the persistence layer is not an authorization bypass.
- Idempotency behavior: `CommitSale` is idempotent on store ID, terminal ID, and local transaction ID; repeated calls return the original outcome.
- Error model: validation, authorization, capacity, lock/timeout, corruption, migration, and transient storage errors use stable codes and never report success after an uncertain commit.

## Events

- No new public event is introduced. The transaction persists the existing domain outbox message for later synchronization.
- Any emitted domain event follows the shared envelope: event ID, aggregate ID, store ID, occurred time, correlation ID, and schema version.
- Delivery and ordering are delegated to the durable outbox and FEAT-003; duplicate consumers must be safe.

## Compatibility

- Additive-change policy: add nullable columns or new schema versions with backward-compatible readers; preserve old outbox envelopes during migration.
- Breaking-change policy: no breaking schema or contract change while old edge binaries may run; require an explicit migration and deployment plan.
- Contract-test location: `tests/Integration/RetailPulse.IntegrationTests` for SQLite behavior and `tests/Contract/RetailPulse.ContractTests` for the outbox envelope.
- Ownership: Edge persistence owns the local schema and migration scripts; Sync owns cloud delivery semantics.
