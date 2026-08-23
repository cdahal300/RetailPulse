# FEAT-013: QA Test Plan

## Test coverage

- Unit tests: source selection, PII/card-data filtering, prompt construction, schema validation, claim/source checks, injection resistance, retry/idempotency, and cost limits.
- Integration tests: analytics inputs, Azure OpenAI sandbox/mock, job store/queue, authorization, audit, timeout/quota, and rejected-output handling.
- Contract tests: insight request/status/result schemas, source links, validation states, event envelopes, and model adapter boundary.
- End-to-end tests: manager request through asynchronous completion, stale source, anomaly explanation, unavailable model, and PWA display.
- PWA or device tests: responsive insight rendering, pending/stale/unavailable states, source navigation, and no sensitive browser cache.
- Performance and resilience tests: queue throughput, model latency, rate limits, token/cost budgets, retry storms, provider outage, and report freshness.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path summary/explanation | Valid insight cites governed source and metadata | Integration/E2E |
| Offline or dependency unavailable | Checkout/sync/reporting continue; insight is pending/unavailable | E2E/Resilience |
| Timeout or retry | Async retry produces one result and bounded cost | Unit/Integration |
| Duplicate request or event | One job/result for request and source version | Contract/Integration |
| Unauthorized access | Wrong role/store/tenant cannot request or view | Security/Integration/PWA |
| Invalid input or conflict | Invalid scope/stale source is rejected or marked stale | Unit/Integration |
| Invalid/unsafe JSON | Output is rejected and not shown as fact | Unit/E2E |
| Prompt injection/PII | Input is filtered; unsafe result unavailable and audited | Security/Integration |
| Model quota/cost limit | Job degrades safely and alerts without blocking core flows | Resilience |

## Release evidence

- Test command: focused insights unit/integration/contract tests, PWA Playwright insight tests, and redaction/security scans.
- Required environment: seeded governed aggregates, Azure OpenAI sandbox/mock, isolated test identities/stores, queue/job store, and cost budget.
- Evidence artifact: source/claim validation report, PII/card-data scan, prompt-injection cases, latency/cost report, and graceful-degradation evidence.
- Known gaps: model behavior, Azure OpenAI quota/region availability, and human review quality require ongoing production evaluation.
