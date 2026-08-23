# FEAT-004: QA Test Plan

## Test coverage

- Unit tests: barcode lookup, effective pricing, tax/category validation, movement balance, thresholds, negative-stock policy, and version conflicts.
- Integration tests: PostgreSQL ledger/read model, SQLite catalog cache, sync ordering, duplicate movements, and reconciliation.
- Contract tests: catalog/inventory commands and v1 domain event envelopes.
- End-to-end tests: offline lookup, sale inventory decrement, manager receipt/adjustment, conflict review, and low-stock notification.
- PWA or device tests: barcode scanner workflows and responsive manager inventory screens on supported Android/iOS/desktop browsers.
- Performance and resilience tests: lookup latency, bulk catalog refresh, movement throughput, stale-cache startup, and delayed-event recovery.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path lookup and movement | Effective product data and auditable balance are correct | Unit/Integration/E2E |
| Offline or dependency unavailable | Cached catalog supports policy; movement queues safely | Integration/E2E |
| Timeout or retry | Refresh/command retries without duplicate movement | Integration |
| Duplicate request or event | One movement and one balance change | Contract/Integration |
| Unauthorized access | Cashier adjustment and cross-store access are denied | Unit/Integration/PWA |
| Invalid input or conflict | Stable error; completed sales are not overwritten | Unit/Integration/E2E |
| Stale price/version | Policy is applied and operator sees effective time | E2E |

## Release evidence

- Test command: focused `dotnet test` for catalog/inventory unit, integration, and contract filters, plus PWA Playwright inventory tests.
- Required environment: PostgreSQL, SQLite, event bus fixture, seeded stores/products, and browser/device matrix.
- Evidence artifact: reconciliation report, conflict cases, stale-data screenshots, and performance timings.
- Known gaps: market-specific tax/fiscal rules and physical scanner models require local certification.
