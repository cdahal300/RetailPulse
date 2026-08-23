# FEAT-007: QA Test Plan

## Test coverage

- Unit tests: pipeline helper scripts, versioning, change detection, migration gates, and rollback decision logic.
- Integration tests: GitHub/Azure identity, registry push/pull, AKS deployment, Key Vault access, health checks, and environment approvals.
- Contract tests: workflow inputs/outputs, image metadata, deployment manifests, API compatibility, and event schemas.
- End-to-end tests: PR validation, build-to-staging, progressive production simulation, failed promotion, and rollback.
- PWA or device tests: run PWA build/accessibility smoke and edge artifact compatibility in the release pipeline.
- Performance and resilience tests: parallel build limits, deployment duration, rollback time, agent failure, registry outage, and retry safety.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path release | Validated digest deploys and passes smoke/approval gates | Integration/E2E |
| Offline or dependency unavailable | Edge artifact remains deployable; cloud stage stops without corrupting release | E2E |
| Timeout or retry | Stage resumes safely with same digest/run context | Integration |
| Duplicate request or event | Rerun does not duplicate migration or rollout | Integration |
| Unauthorized access | Unapproved branch/identity cannot deploy production | Security/Integration |
| Invalid input or conflict | Scan, policy, or compatibility gate blocks promotion | Unit/Integration |
| Failed deployment | Promotion stops and rollback restores health | E2E/Resilience |

## Release evidence

- Test command: repository pipeline commands for `dotnet test`, PWA checks, contract tests, security scans, IaC validation, and deployment smoke tests.
- Required environment: GitHub/Azure test projects, disposable registry/AKS namespace, and protected staging approval.
- Evidence artifact: pipeline run, scan reports, image digest, approvals, migration report, health checks, and rollback result.
- Known gaps: production provider outages and emergency change paths require operational rehearsal.
