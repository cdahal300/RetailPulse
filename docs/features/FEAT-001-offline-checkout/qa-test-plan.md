# FEAT-001 QA Test Plan

## Test projects

- `tests/Unit/RetailPulse.UnitTests`: cart, totals, payment mapping, state transitions, idempotency.
- `tests/Integration/RetailPulse.IntegrationTests`: SQLite transaction, outbox, restart recovery, and sync behavior.
- `tests/Contract/RetailPulse.ContractTests`: command and `SaleCompleted.v1` compatibility.
- `tests/EndToEnd/`: full cashier workflow with the fake payment adapter.

## Required scenario matrix

| Scenario | Expected result |
|---|---|
| Online approved payment | Sale commits locally and synchronizes |
| Cloud unavailable after provider approval | Sale commits locally and remains sync-pending |
| Payment declined | No completed sale is created |
| Payment timeout | Sale remains unpaid; retry is safe |
| Process restart after local commit | Sale and outbox survive |
| Duplicate sync delivery | One cloud sale, same result returned for retry |
| Cloud unavailable | Checkout remains available |
| Local database failure | No completed receipt is produced |
| Local storage nearly full | Checkout fails safely with an actionable message |
| Invalid product or quantity | Cart rejects the operation |
| Cashier manager-only action | Authorization is denied |
| Sensitive-data scan | No card data in storage, logs, events, or telemetry |
