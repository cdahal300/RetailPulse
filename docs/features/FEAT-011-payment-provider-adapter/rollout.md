# FEAT-011: Rollout and Operations

## Release

- Feature flag: `payments.provider.v1` gates provider adapter activation; authorization, audit, and provider controls cannot be bypassed.
- Safe default: sandbox/mock adapter or existing approved payment path; uncertain outcomes remain pending, never approved by fallback.
- Migration strategy: deploy adapter and reconciliation schema additively; support opaque references and old payment states until all pending transactions settle.
- Deployment order: secrets/provider config, adapter, reconciliation worker, edge image, sandbox, staging terminal, certified pilot, then store cohorts.
- Approval gates: payment/provider owner, security/PCI, finance, QA, operations, and provider certification approval.

## Rollout

- Targeting plan: sandbox, internal terminal, one pilot store/terminal model, then provider/store cohorts.
- Metrics: approval/decline/pending rates, terminal latency, timeout/duplicate risk, reconciliation age, refund outcomes, provider errors, and redaction violations.
- Alerts and runbooks: alert on unknown/pending age, duplicate risk, provider outage, terminal disconnect, refund failure, and card-data detection; link payment reconciliation runbook.
- Expansion criteria: certification complete, no sensitive-data findings, stable approval/reconciliation metrics, and tested outage recovery.

## Rollback

- First action: disable adapter activation and route new transactions to the approved safe path or decline/pending policy.
- Data and event handling: preserve opaque references and pending states; reconcile with provider before retrying or refunding.
- Deployment rollback: revert adapter only if it can read current payment states; otherwise deploy a compatible forward fix.
- Recovery validation: compare provider settlement/status, local payment state, sale state, audit, and event counts.

## Ownership

- Feature owner: Payments integration team.
- On-call owner: Payment operations with provider support.
- Expiry or cleanup issue: remove sandbox credentials, temporary provider mappings, and activation flag after certified production adoption.
