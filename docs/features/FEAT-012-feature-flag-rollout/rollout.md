# FEAT-012: Rollout and Operations

## Release

- Feature flag: each new behavior uses a stable flag; new flags are off by default and activation is separate from deployment.
- Safe default: deterministic disabled behavior when provider, snapshot, identity, signature, or targeting data is unavailable.
- Migration strategy: deploy provider-neutral abstraction and compatible snapshot schema first; retain old snapshots and both flag states during rollout.
- Deployment order: abstraction/evaluation, Azure App Configuration, audit/approval controls, authenticated edge snapshot, application flag checks, then pilot activation.
- Approval gates: feature owner, release owner, security, operations, QA, and separate production flag approver.

## Rollout

- Targeting plan: development, staging, internal users, one pilot store/terminal/role, then percentage/store cohorts; never target by unreviewed broad default.
- Metrics: evaluation errors, stale/invalid snapshots, provider availability, convergence time, targeting distribution, checkout continuity, and business/technical errors.
- Alerts and runbooks: alert on provider outage, signature failure, stale snapshot, unexpected scope, change burst, and rollback failure; link flag operations runbook.
- Expansion criteria: audit/approval completeness, safe fallback test, targeting verification, stable telemetry, and successful emergency disable.

## Rollback

- First action: disable the flag through the approved emergency path; edge falls back locally if cloud is unavailable.
- Data and event handling: preserve flag history, approvals, snapshots, and evaluation evidence; do not alter authorization/payment/audit controls.
- Deployment rollback: usually unnecessary; revert image only for code defects after confirming flag-off compatibility.
- Recovery validation: verify disabled evaluations across scopes, checkout continuity, snapshot convergence, audit, and no unauthorized behavior.

## Ownership

- Feature owner: Release/platform engineering.
- On-call owner: Platform operations.
- Expiry or cleanup issue: every flag has an owner and expiry; remove dead branches, keys, snapshots, and temporary targeting after adoption.
