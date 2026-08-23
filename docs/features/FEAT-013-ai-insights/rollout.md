# FEAT-013: Rollout and Operations

## Release

- Feature flag: `ai.insights.v1` gates insight requests/display; AI remains asynchronous and cannot enter checkout, sync, authorization, payment, or inventory mutation paths.
- Safe default: disabled or unavailable state with deterministic reports and no blocking; invalid output is never shown as trusted advice.
- Migration strategy: provision insight/job/audit schema additively; version prompt, model adapter, result schema, and source aggregate contract.
- Deployment order: governed aggregates and quality checks, job/orchestration service, validator/redactor, Azure OpenAI adapter, API/read model, PWA display, pilot activation.
- Approval gates: AI/data owner, security/privacy, legal/compliance as applicable, finance for cost, QA, operations, and separate production activation approval.

## Rollout

- Targeting plan: internal synthetic data, staging, internal managers, one pilot tenant/store, then controlled cohorts by insight type.
- Metrics: job success/failure, stale-source rate, validation rejection, source citation coverage, latency, queue age, token/cost usage, PII detections, and user feedback.
- Alerts and runbooks: alert on unsafe/PII output, validation spike, provider outage/quota, cost anomaly, queue age, and source freshness; link AI incident/runbook.
- Expansion criteria: redaction/injection tests pass, source claims are grounded, cost/latency budgets hold, and graceful unavailability is verified.

## Rollback

- First action: disable `ai.insights.v1` requests/display and stop new model jobs; keep governed reports available.
- Data and event handling: preserve audit/rejection evidence and source aggregates; quarantine questionable insights and do not auto-delete evidence.
- Deployment rollback: revert adapter/prompt/model version only after pending jobs are stopped or safely versioned; result readers remain compatible.
- Recovery validation: verify no core transaction impact, source/claim validation, authorization, audit, cost controls, and safe reprocessing.

## Ownership

- Feature owner: Analytics/AI insights team.
- On-call owner: Cloud AI/data operations.
- Expiry or cleanup issue: review prompt/model/flag expiry, remove obsolete versions, and retain only governed insight/audit records per policy.
