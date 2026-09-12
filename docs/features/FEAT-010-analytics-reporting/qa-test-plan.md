# FEAT-010: QA Test Plan

## Test coverage

- Unit tests: event-to-fact mapping, deduplication, aggregation, timezone/currency rules, freshness, correction, and access filters.
- Replay command tests: owner authorization, command idempotency, correction result, and audit/job durability.
- Integration tests: Service Bus/stream ingestion, ADLS Gen2 partitions, PostgreSQL/read models, replay, retention, and query authorization.
- Contract tests: consumed event versions, report schemas, freshness/status fields, export format, and FEAT-013 input contract.
- End-to-end tests: sale through sync through aggregate/dashboard, late event correction, report export, and tenant/store isolation.
- PWA or device tests: manager/owner report rendering, responsive tables, stale/partial indicators, and export download behavior.
- Performance and resilience tests: ingestion throughput, query latency, partition pruning, late-event replay, backlog recovery, and cost/cardinality limits.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path report | Totals and source/freshness metadata are correct | Integration/E2E |
| Offline or dependency unavailable | Delayed sync is labeled; checkout remains unaffected | E2E/Resilience |
| Timeout or retry | Pipeline retry/reprocess converges deterministically | Integration |
| Duplicate request or event | Revenue/inventory are counted once | Contract/Integration |
| Unauthorized access | Cross-store/tenant query/export is denied | Integration/PWA |
| Invalid input or conflict | Invalid schema/currency/time range is quarantined or rejected | Unit/Integration |
| Late/corrected event | Aggregate is corrected with audit/source link | Integration/E2E |

## Release evidence

- Test command: focused analytics unit/integration/contract tests and PWA Playwright report tests.
- Required environment: disposable Service Bus/stream, ADLS Gen2, PostgreSQL/read model, seeded multi-tenant data, and timezone/currency fixtures.
- Evidence artifact: reconciliation report, duplicate/late-event results, freshness dashboard, authorization report, and query benchmark.
- Known gaps: production-scale ADLS cost and regional replay require staging load rehearsal.

## Verified Dev Slice

- Unit tests cover sale-event mapping, source-event deduplication, tenant/store filtering, aggregation, and freshness metadata.
- Dev deployment passed AKS rollout and health smoke tests.
- Authenticated dev seed plus PWA report query returned `event-facts` with complete freshness metadata.
- Release evidence still requires durable stream/ADLS replay, late-event correction, multi-tenant integration tests, and performance benchmarking.
- The current replay implementation is an in-process command boundary; production approval requires durable job state and audit records.
