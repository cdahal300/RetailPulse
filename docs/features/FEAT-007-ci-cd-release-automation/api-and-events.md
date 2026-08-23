# FEAT-007: API and Event Contracts

## APIs and commands

- Pipeline commands: validate, build, scan, publish immutable image, deploy by digest, run compatible migrations, verify health, promote, and rollback.
- No new public RetailPulse business API or domain event is introduced by CI/CD.
- Authentication and authorization: GitHub/Azure federated identity, protected environments, registry permissions, AKS RBAC, and named approvers.
- Idempotency behavior: pipeline run IDs and artifact digests make retries safe; migrations use application-level idempotency/locking.
- Error model: each stage reports stable failure category, commit, artifact digest, environment, and remediation link.

## Events

- No new public domain events are introduced. Deployment and release records are operational audit artifacts.
- Existing application event/API contracts must pass compatibility checks before promotion.
- Ownership: release engineering owns pipeline contracts; feature teams own application contracts.

## Compatibility

- Additive-change policy: add pipeline checks, stages, and optional inputs without changing required artifact or deployment interfaces.
- Breaking-change policy: version reusable workflows, deployment manifests, and migration procedures; support old images during rollout.
- Contract-test location: pipeline contract tests and `tests/Contract/RetailPulse.ContractTests`; deployment smoke checks run against staging.
