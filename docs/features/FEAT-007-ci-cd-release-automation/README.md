# FEAT-007: CI/CD and Release Automation

## Outcome

As an engineering team, we want every change built, tested, scanned, and deployable so that releases are repeatable and reversible.

## Scope

- Pull-request validation for .NET, PWA, contracts, security, and formatting.
- Immutable container builds and vulnerability scanning.
- Push to Azure Container Registry.
- AKS deployment to development, staging, and production with approvals.
- Database migration checks, health verification, progressive rollout, and rollback.
- Feature-flag activation kept separate from deployment.

## Acceptance criteria

- A pull request cannot merge when required tests or security checks fail.
- Deployments use immutable image digests and record commit identity.
- Production requires approval and verifies readiness/liveness checks.
- Failed deployment automatically stops promotion and exposes rollback instructions.
- No cloud credentials or kubeconfig files appear in logs or artifacts.

## Dependencies and QA

Depends on FEAT-006 and health/telemetry from FEAT-008. Test failed builds, migration compatibility, image scan failures, deployment rollback, and disabled feature flags.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
