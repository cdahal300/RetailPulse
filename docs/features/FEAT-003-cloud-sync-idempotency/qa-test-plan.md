# FEAT-003: QA Test Plan

## Test coverage

- Unit tests: idempotency key generation, retry classification/backoff, state transitions, conflict policy, and dead-letter decisions.
- Integration tests: SQLite outbox to cloud API, Service Bus retry/dead-letter, durable deduplication, reconnect, ordering, and partial outage.
- Contract tests: sync request/response and all published event v1 envelopes.
- End-to-end tests: offline sale, reconnect, duplicate delivery, operator review, and reconciliation.
- PWA or device tests: supported edge device connectivity transitions and sync-health display; PWA smoke only where the health query is consumed.
- Performance and resilience tests: throughput per store, oldest-message latency, Service Bus backlog, throttling, timeout storms, and recovery after outage.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path sync | Sale is accepted once and outbox becomes synced | Integration/E2E |
| Offline or dependency unavailable | Local outbox remains durable and sync resumes later | E2E/Resilience |
| Timeout or retry | Backoff retries without losing the message | Unit/Integration |
| Duplicate request or event | One cloud sale and one downstream effect | Contract/Integration |
| Unauthorized access | Wrong store/device is rejected | Integration |
| Invalid input or conflict | Reviewable state with no silent overwrite | Unit/Integration |
| Service Bus dead-letter | Operator can inspect and replay safely | Integration/E2E |

## Release evidence

- Test command: `dotnet test tests/Integration/RetailPulse.IntegrationTests` and `dotnet test tests/Contract/RetailPulse.ContractTests` with sync filters.
- Required environment: disposable Service Bus namespace or emulator-equivalent, cloud database, and an offline edge fixture.
- Evidence artifact: duplicate-delivery report, outage recovery timings, dead-letter/replay evidence, and contract results.
- Known gaps: production Service Bus quota and regional-failover behavior require staging validation.
