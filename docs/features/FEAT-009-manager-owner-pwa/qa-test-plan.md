# FEAT-009: QA Test Plan

## Test coverage

- Unit tests: role-aware rendering, cache freshness, command state machine, service-worker update handling, validation, and notification preferences.
- Integration tests: API auth, read models, command idempotency, push provider, service-worker assets, and cache invalidation.
- Contract tests: API response/error schemas, command status, notification payloads, and event-derived read models.
- End-to-end tests: sign-in, role routes, offline launch, reconnect, pending/confirmed command, notifications, logout, and accessibility.
- PWA or device tests: Playwright desktop/tablet, real iOS Safari and Android Chrome smoke, install/update, viewport/orientation, keyboard/touch, and screen reader checks.
- Performance and resilience tests: first load, cache startup, bundle size, API latency, offline transitions, push failure, and service-worker upgrade.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path manager/owner flow | Authorized dashboard and command complete with confirmed status | PWA E2E |
| Offline or dependency unavailable | Stale reads are labeled; unsupported mutations are blocked; supported ones queue | PWA/Integration |
| Timeout or retry | One command result after reconnect and retry | Contract/E2E |
| Duplicate request or event | UI converges without duplicate action | Integration/E2E |
| Unauthorized access | Route, API, cache, and cross-store access are denied | PWA/Integration |
| Invalid input or conflict | Accessible error and reviewable status are shown | Unit/E2E |
| Service-worker update | Version changes without corrupting cache or pending state | Device/E2E |

## Release evidence

- Test command: PWA package test/lint/build, Playwright suites, accessibility checks, and focused contract tests.
- Required environment: staging cloud APIs, test identities/roles/stores, HTTPS origin, push test service, and iOS/Android devices.
- Evidence artifact: browser/device matrix, accessibility report, cache/offline recording, bundle metrics, and auth denial report.
- Known gaps: browser push support and background behavior vary by OS/version and require a maintained support matrix.
