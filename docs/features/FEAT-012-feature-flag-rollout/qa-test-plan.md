# FEAT-012: QA Test Plan

## Test coverage

- Unit tests: deterministic targeting, safe defaults, context validation, version comparison, expiry, authorization separation, and snapshot signature validation.
- Integration tests: Azure App Configuration, managed identity, audit store, snapshot distribution/cache, provider outage, stale/tampered snapshot, and rollback.
- Contract tests: flag admin commands, evaluation result, snapshot schema/signature, change events, and OpenFeature-compatible adapter behavior.
- End-to-end tests: deploy disabled, approve/activate pilot, evaluate by store/role/terminal, expand, emergency disable, and cleanup.
- PWA or device tests: edge offline evaluation, startup with stale snapshot, terminal/store targeting, and PWA behavior when flag changes.
- Performance and resilience tests: evaluation latency, snapshot size, cache startup, high-cardinality targeting, provider outage, and activation convergence.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path activation | Approved targeted behavior activates without redeploy | Integration/E2E |
| Offline or dependency unavailable | Edge uses safe deterministic default/snapshot; checkout continues | Device/Integration |
| Timeout or retry | Change publish/evaluation retry is safe and convergent | Unit/Integration |
| Duplicate request or event | One versioned flag change and snapshot effect | Contract/Integration |
| Unauthorized access | Unapproved production change and cross-scope evaluation are denied | Security/E2E |
| Invalid input or conflict | Invalid target/stale version/tamper is rejected | Unit/Integration |
| Targeting mistake | Emergency disable converges within documented window | E2E/Resilience |
| Expiry/cleanup | Expired flag cannot activate and history remains auditable | Integration |

## Release evidence

- Test command: focused flag unit/integration/contract tests, edge tests, PWA smoke, and deployment pipeline flag-separation checks.
- Required environment: Azure App Configuration staging, managed identity, signing/key fixture, multiple stores/roles/terminals, and disconnected edge fixture.
- Evidence artifact: targeting matrix, approval/audit report, snapshot verification, outage/rollback timing, and checkout continuity result.
- Known gaps: production App Configuration quotas, key rotation, and cross-region behavior require platform rehearsal.
