# FEAT-008: Rollout and Operations

## Release

- Feature flag: `observability.otel.v1` controls optional exporter/dashboard activation; local error telemetry remains bounded and non-blocking.
- Safe default: redact first, sample conservatively, cap buffers/cardinality, and never block checkout on export.
- Migration strategy: deploy instrumentation and schemas before dashboards/alerts; support old/new metric names during collector migration.
- Deployment order: redaction/context library, Edge/Cloud/workers, collector/exporters, Azure Monitor workspace, dashboards/alerts, then PWA diagnostics.
- Approval gates: observability owner, security/privacy, platform, QA, and on-call owners approve redaction and alert runbooks.

## Rollout

- Targeting plan: development, staging, one pilot store, then all runtimes by environment and workload.
- Metrics: export success/drop rate, trace completeness, collector queue, telemetry overhead, cardinality, local buffer size, and alert noise.
- Alerts and runbooks: checkout error, sync age, payment outcome, API latency, AI latency, exporter failure, and buffer saturation with runbook links.
- Expansion criteria: verified redaction, trace continuity, bounded overhead/storage, and actionable alert tests.

## Rollback

- First action: disable remote export or high-volume instrumentation while preserving local business behavior.
- Data and event handling: retain business events; discard only expired telemetry according to retention, never raw sensitive payloads.
- Deployment rollback: revert instrumentation/exporter version while keeping compatible dashboards or restore previous dashboard rules.
- Recovery validation: verify checkout/sync latency, redaction, context propagation, exporter recovery, and alert delivery.

## Ownership

- Feature owner: Observability/platform team.
- On-call owner: Cloud operations.
- Expiry or cleanup issue: remove temporary sampling overrides, dashboards, alerts, and migration metric aliases after adoption.
