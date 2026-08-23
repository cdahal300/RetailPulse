# FEAT-008: QA Test Plan

## Test coverage

- Unit tests: context propagation, metric calculations, redaction, sampling, bounded buffering, and alert rule evaluation.
- Integration tests: Edge/Cloud/worker OTLP export, Azure Monitor/Application Insights, collector outage, and local buffer drain.
- Contract tests: telemetry attribute names/units, correlation propagation, alert payloads, and runbook links.
- End-to-end tests: trace one online/offline sale through sync, dashboard queries, alert delivery, and incident correlation.
- PWA or device tests: browser API-call correlation, offline telemetry behavior, service-worker logging limits, and supported device diagnostics.
- Performance and resilience tests: instrumentation overhead, high-cardinality protection, log/trace volume, exporter throttling, and storage bounds.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path trace | Sale and sync spans correlate across edge/cloud/worker | Integration/E2E |
| Offline or dependency unavailable | Edge remains observable with bounded local buffer | Integration/Device |
| Timeout or retry | Retry spans are linked without changing business outcome | Unit/Integration |
| Duplicate request or event | Telemetry grouping does not imply duplicate sale | Contract/Integration |
| Unauthorized access | Dashboard/diagnostic access is scoped and denied appropriately | Integration/PWA |
| Invalid input or conflict | Error attributes are safe and actionable | Unit/Integration |
| Collector outage/high volume | Business latency remains within budget and buffer is bounded | Resilience |

## Release evidence

- Test command: focused runtime tests, integration observability filters, contract tests, and PWA Playwright telemetry smoke checks.
- Required environment: OTLP collector fixture plus Azure Monitor/Application Insights staging workspace with alert receiver.
- Evidence artifact: redaction report, trace sample, alert firing/resolution evidence, overhead benchmark, and buffer recovery report.
- Known gaps: production cardinality and retention costs require post-deployment review.
