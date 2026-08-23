# FEAT-013: API and Event Contracts

## APIs and commands

- Command: `POST /api/v1/insights` accepts insight type, authorized store scope, aggregate/source window, request ID, and freshness policy; returns job ID/status.
- Query: `GET /api/v1/insights/{insightId}` returns schema-valid insight, source links, freshness, validation status, and model/prompt metadata.
- No AI call is on the checkout or synchronization critical path; Azure OpenAI is behind an internal insights service.
- Authentication and authorization: server-side manager/owner and tenant/store policy; minimum necessary source data is selected before model submission.
- Idempotency behavior: request ID plus source aggregate version deduplicates jobs/results; retries return the existing job/result.
- Error model: stable forbidden, stale-source, unavailable, rate-limited, validation-failed, unsafe-output, unsupported, and reviewable states.

## Events

- Consumes `ReportRefreshCompleted.v1`, `LowStockDetected.v1`, and `DataQualityIssueDetected.v1` when applicable.
- May publish `InsightRequested.v1`, `InsightCompleted.v1`, and `InsightRejected.v1` for workflow/audit/notification consumers.
- Producer: insights service; consumers: PWA/read models, notifications, audit, and cost monitoring.
- Required metadata: event ID, aggregate ID, store ID, occurred time, correlation ID, and schema version; include source references and validation status, never sensitive prompts/raw card data.
- Delivery and ordering: durable asynchronous jobs; source aggregate version and insight version prevent stale completion from replacing newer data.
- Duplicate handling: deduplicate request/job/event IDs and use compare-and-set on source version.

## Compatibility

- Additive-change policy: add optional insight fields and new insight types; clients treat unavailable/validation states explicitly.
- Breaking-change policy: version insight schema, prompt contract, and model adapter; maintain previous result schema during client propagation.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; pipeline/provider tests in `tests/Integration/RetailPulse.IntegrationTests`.
- Ownership: Analytics owns source aggregates; insights owns orchestration, validation, provider adapter, and audit; PWA owns display.
