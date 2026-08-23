# FEAT-008: API and Event Contracts

## APIs and commands

- Telemetry interfaces: OpenTelemetry instrumentation for traces, metrics, and structured logs; health/diagnostic endpoints expose aggregate status only.
- No new public business API or domain event is introduced; telemetry uses OTLP-compatible export to Azure Monitor/Application Insights.
- Authentication and authorization: exporter credentials use managed identity/Key Vault; dashboards and diagnostic endpoints require scoped access.
- Idempotency behavior: telemetry retries may duplicate records; correlation/span IDs and backend deduplication/grouping prevent business interpretation as duplicate transactions.
- Error model: exporter failures are non-blocking, sampled, logged locally with bounded backoff, and reported through health metrics.

## Events

- No new public domain events are introduced. Existing domain events carry correlation metadata; telemetry spans/logs observe them.
- Required context propagation: event ID, aggregate ID, store ID, occurred time, correlation ID, schema version, local transaction ID, and deployment version where available.
- Ownership: each runtime owns instrumentation correctness; platform observability owns exporters, dashboards, alerts, and redaction policy.

## Compatibility

- Additive-change policy: add optional attributes and metrics with bounded cardinality; preserve existing names and units.
- Breaking-change policy: version metric/log schemas and dashboards; provide overlap during collector/exporter migration.
- Contract-test location: observability integration tests in `tests/Integration/RetailPulse.IntegrationTests`; event contracts in `tests/Contract/RetailPulse.ContractTests`.
