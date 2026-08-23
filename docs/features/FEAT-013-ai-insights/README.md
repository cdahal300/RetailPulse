# FEAT-013: AI Insights

## Outcome

As a manager, I want concise explanations of sales, stock, and anomalies so that I can decide what to investigate next.

## Scope

- Sales summaries from curated aggregates.
- Low-stock explanations with supporting values.
- Deterministic anomaly signals with LLM-generated explanations.
- Azure OpenAI integration behind an insights service.
- PII filtering, schema validation, prompt/model version audit, and source links.

## Acceptance criteria

- AI is asynchronous and never blocks checkout or synchronization.
- Only minimum aggregated data is sent to the model.
- Invalid, unsafe, or unsupported output is rejected or marked unavailable.
- Every insight records source data reference, prompt version, model deployment, output, and validation result.
- AI cannot approve payments, set prices, mutate inventory, or bypass authorization.

## Dependencies and QA

Depends on FEAT-008 and FEAT-010. Test stale data, model timeout, invalid JSON, prompt injection in source fields, PII leakage, unsupported claims, cost limits, and graceful unavailability.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
