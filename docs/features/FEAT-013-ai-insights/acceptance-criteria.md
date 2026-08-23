# FEAT-013: Acceptance Criteria

## Functional behavior

- Given governed sales, inventory, or anomaly aggregates, when an authorized manager requests an insight, then an asynchronous job returns a schema-valid summary/explanation with source values and freshness.
- Given a deterministic anomaly signal, when explanation generation runs, then the result explains the signal and cites supporting data without inventing unsupported claims.
- Given a completed insight, when displayed, then source reference, prompt version, model deployment, validation result, and generated time are available.

## Failure and resilience behavior

- Given Azure OpenAI timeout, quota, outage, invalid JSON, unsafe output, or stale source data, then the insight is unavailable/marked stale or reviewable and checkout, sync, and core reporting continue.
- Given duplicate job delivery or retry, then one insight version/result is retained for the source and request identity.
- Given prompt injection or adversarial source text, then source content is treated as untrusted data, instructions are ignored, and the result is rejected or safely constrained.

## Authorization and isolation

- Only authorized manager/owner roles can request or view insights for their tenant/store; source data and prompts are scope-filtered server-side.
- AI cannot approve payments, set prices, mutate inventory, grant access, or bypass audit/authorization; model output is advisory only.

## Data and security

- Sensitive data handling: send minimum aggregated, de-identified data; filter PII and all payment-card data before Azure OpenAI; do not include PAN, CVV, PIN, magnetic-stripe, or raw card data.
- Audit requirements: retain source references, prompt/template version, model deployment/version, request/response status, validation result, actor, and cost metadata.
- Retention and deletion: apply insight/prompt/audit retention and tenant deletion policy; avoid retaining provider payloads beyond the approved record.
- Validate output against a strict schema, apply content/safety and claim/source checks, protect keys with managed identity/Key Vault, and enforce cost/rate limits.
