# FEAT-002: QA Test Plan

## Test coverage

- Unit tests: transaction assembly, idempotency key handling, schema-version selection, error mapping, and retention rules.
- Integration tests: SQLite atomic commit/rollback, restart recovery, concurrent lock handling, migration upgrade/retry, corruption detection, backup restore, and storage-full behavior.
- Contract tests: persisted outbox envelope and compatibility with FEAT-003 sync input.
- End-to-end tests: offline sale, process restart before sync, reconnect, and receipt/outbox recovery.
- PWA or device tests: edge hardware-independent smoke tests on supported OS filesystems; no PWA change is in scope.
- Performance and resilience tests: commit latency at target register load, bounded database growth, retry after lock, and recovery after abrupt termination.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path sale commit | All required records commit atomically and are recoverable | Integration |
| Offline or dependency unavailable | Local commit succeeds without cloud access; outbox remains pending | E2E/Integration |
| Timeout or retry | Retry returns one result for the local transaction identity | Unit/Integration |
| Duplicate request or event | No duplicate sale or outbox message is created | Integration/Contract |
| Unauthorized access | Wrong store or invalid local session is rejected and audited | Unit/Integration |
| Invalid input or conflict | Validation fails with no partial writes | Unit/Integration |
| Full, locked, or corrupt database | Checkout does not claim success; health exposes remediation | Integration/Resilience |
| Interrupted migration | Startup can safely retry and reports schema state | Integration |

## Release evidence

- Test command: `dotnet test tests/Integration/RetailPulse.IntegrationTests` plus the focused SQLite test filter.
- Required environment: .NET SDK from `global.json`; temporary SQLite database and controlled filesystem fault injection.
- Evidence artifact: test results, migration version report, restart-recovery log, and storage-fault report.
- Known gaps: production filesystem encryption and hardware-specific storage behavior require environment validation.
