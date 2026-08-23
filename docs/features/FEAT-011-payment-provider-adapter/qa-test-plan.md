# FEAT-011: QA Test Plan

## Test coverage

- Unit tests: amount/currency validation, state machine, idempotency, provider error mapping, redaction, cancellation, and reconciliation decisions.
- Integration tests: provider sandbox, certified terminal simulator/hardware, timeout/reconnect, duplicate authorization, status recovery, refund, and audit.
- Contract tests: adapter interface, provider mapping, payment event envelope, and prohibited-field assertions.
- End-to-end tests: online/offline edge checkout with approved/declined/pending/refund flows and cloud reconciliation.
- PWA or device tests: supported terminal/device models, edge OS connectivity, and manager refund status display; PWA never handles card data.
- Performance and resilience tests: terminal response latency, concurrent transactions, provider throttling/outage, retry storms, and reconciliation backlog.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path authorization/refund | Correct status and opaque reference are stored | Integration/E2E |
| Offline or dependency unavailable | Checkout follows pending/decline policy; no false approval | E2E/Resilience |
| Timeout or retry | Status recovery prevents double charge | Unit/Integration |
| Duplicate request or event | One provider charge and one business effect | Contract/Integration |
| Unauthorized access | Unapproved refund/device/store is denied | Unit/Integration |
| Invalid input or conflict | Amount/currency/terminal mismatch is rejected/reviewable | Unit/Integration |
| Sensitive-data probe | Card-like data is rejected/redacted from all outputs | Security/Contract |
| Provider certification | Required terminal/provider scenarios pass | Hardware/Sandbox |

## Release evidence

- Test command: focused adapter unit/integration/contract tests and edge checkout tests; provider certification suite per vendor.
- Required environment: provider sandbox, certified terminal/device, test merchant, isolated credentials, and FEAT-008 redaction telemetry.
- Evidence artifact: certification result, status/retry matrix, no-card-data scan, reconciliation report, and failure injection results.
- Known gaps: production acquirer behavior, chargeback flows, and provider fees/limits remain external dependencies.
