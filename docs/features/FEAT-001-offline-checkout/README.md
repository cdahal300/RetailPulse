# FEAT-001: Reliable Checkout and Cloud Recovery

## Status

Proposed

## User outcome

As a cashier, I want checkout to remain responsive and recover safely when the RetailPulse cloud is temporarily unavailable so that I do not lose cart or transaction state.

## Scope

- Search the locally available product catalog.
- Add products to a cart and calculate the sale total locally.
- Request payment through an injected payment-provider adapter.
- After a valid external payment-provider result, commit the sale, payment reference, inventory movement, and outbox event in one local transaction.
- Print or display a receipt after the local commit succeeds.
- Show online, cloud-unavailable, payment-pending, and sync-pending status.
- Synchronize a locally committed sale later using an idempotency key.

## Non-goals

- Building a payment processor, card network, acquirer, or settlement system.
- Storing PAN, CVV, PIN, magnetic-stripe data, or raw card data.
- Implementing cloud inventory reconciliation beyond accepting the sale event.
- Allowing the manager PWA to perform checkout.
- Supporting arbitrary offline refunds in the first slice.
- Certified offline card authorization; this is a future provider-specific feature.

## Dependencies

- Related architecture: `docs/architecture/overview.md`
- Related ADRs: `docs/architecture/decisions/002-offline-first-edge.md`, `docs/architecture/decisions/003-external-payment-provider.md`
- Related issues: TBD
- External providers: fake payment adapter for development; selected certified terminal/PSP for integration testing

## Architecture impact

- Owning boundary: Edge
- Offline behavior: Local catalog, SQLite, device gateway, cart recovery, and safe retry remain available when the cloud is unavailable; payment approval still depends on the external provider.
- Payment boundary impact: Adapter only; provider certification and terminal behavior remain external.
- Data model or migration: Add local sales, sale lines, payment references, inventory movements, outbox messages, and sync status.
- Events and API contracts: Introduce `SaleCompleted.v1` and an idempotent cloud sync command.
- Feature flag: Required for pilot rollout; default off until the local and provider-adapter tests pass.

## Acceptance criteria

See [acceptance-criteria.md](acceptance-criteria.md).

## QA coverage

- Unit tests: cart totals, tax/rounding policy, sale state transitions, idempotency key generation, and payment result mapping.
- Integration tests: SQLite transaction atomicity, outbox creation, restart recovery, and sync retry behavior.
- Contract tests: `SaleCompleted.v1` payload compatibility and duplicate command handling.
- End-to-end tests: scan, cart, payment approval, local commit, receipt, reconnect, and cloud acceptance.
- PWA/device coverage: Not applicable to checkout; verify the PWA cannot access checkout endpoints.
- Performance or resilience coverage: cloud outage, provider timeout, process restart, full local storage, duplicate delivery, and sync conflict.

## Rollout

See [rollout.md](rollout.md).

## Delivery links

- Pull request: TBD
- Release: TBD
- Post-release validation: TBD
