# FEAT-008: OpenTelemetry Observability

## Status

Deferred after the first successful dev AKS deployment so MVP feature delivery can continue. This is acceptable for internal development only; production traffic, external pilots, and operational dashboards remain blocked until FEAT-008 or equivalent telemetry and alerting is complete.

## Outcome

As an operator, I want to understand checkout, synchronization, API, and AKS health so that failures are detected and recovered quickly.

## Scope

- OpenTelemetry traces, metrics, and structured logs across Edge, Cloud, workers, and PWA API calls.
- Correlation ID, local transaction ID, store ID, and deployment version propagation.
- Azure Monitor/Application Insights export with portable OTLP support.
- Dashboards and alerts for checkout errors, outbox depth, sync age, payment adapter outcomes, API latency, and AI latency.
- PII and payment-data redaction.

## Acceptance criteria

- One sale can be followed from local checkout through sync and cloud processing.
- Offline operation remains observable without a cloud connection and exports later where supported.
- Logs contain no PAN, CVV, PIN, or raw card data.
- Alerts identify actionable thresholds with runbook links.
- Telemetry overhead and local storage are bounded.

## Dependencies and QA

Instrument before production rollout. Test trace propagation, disconnected edge behavior, redaction, sampling, alert delivery, and high-volume log handling.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
