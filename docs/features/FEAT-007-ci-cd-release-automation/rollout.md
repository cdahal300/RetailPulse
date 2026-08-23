# FEAT-007: Rollout and Operations

## Release

- Feature flag: deployment is separate from application flags; pipeline must not enable business behavior implicitly.
- Safe default: immutable image, disabled new behavior, protected production environment, and no promotion on missing evidence.
- Migration strategy: preflight backward compatibility, run additive migrations before activation, and verify old/new image coexistence.
- Deployment order: validation/scans, build/sign/publish, development, test, staging, production canary, progressive expansion, then separate flag approval.
- Approval gates: code owners, security, QA, platform, data owner for migrations, and production change approver.

## Rollout

- Targeting plan: environment and canary namespace/store, then controlled AKS workload percentage; feature flags target business users separately.
- Metrics: pipeline duration/failure, deployment health, pod readiness, API latency/errors, migration status, image provenance, and rollback rate.
- Alerts and runbooks: alert on failed promotion, unhealthy pods, scan findings, migration lock/failure, and rollback; link release runbook.
- Expansion criteria: clean scans, compatible migrations, stable health/telemetry, approved evidence, and tested rollback.

## Rollback

- First action: stop promotion and route traffic to the previous known-good digest.
- Data and event handling: do not reverse committed business data automatically; keep migrations/events compatible and reconcile if needed.
- Deployment rollback: redeploy prior digest or use progressive rollback; disable the feature flag separately.
- Recovery validation: verify health, API/event compatibility, migration state, sync, checkout, and audit evidence.

## Ownership

- Feature owner: Release engineering.
- On-call owner: Platform/release operations.
- Expiry or cleanup issue: remove temporary pipeline bypasses, canary resources, and obsolete workflow versions.
