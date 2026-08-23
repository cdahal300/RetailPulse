# FEAT-007: Acceptance Criteria

## Functional behavior

- Given a pull request, when validation runs, then .NET, PWA, contract, security, formatting, IaC, and documentation checks report required results before merge.
- Given an approved commit, when the pipeline builds, then it produces immutable versioned artifacts, records commit identity, and deploys by environment.
- Given a production release, when readiness/liveness and smoke checks pass, then promotion requires the documented approval and supports progressive rollout.

## Failure and resilience behavior

- Given a test, scan, migration compatibility, or policy failure, then promotion stops and exposes actionable evidence without deploying the failed artifact.
- Given a deployment timeout or unhealthy workload, then traffic expansion stops, rollback instructions are available, and the previous known-good image remains usable.
- Given a pipeline rerun, then it is safe and does not duplicate migrations, releases, or flag activation.

## Authorization and isolation

- Pull-request, build, registry, Azure, AKS, and production approval permissions are separated by role and environment.
- Production deployment cannot be triggered by untrusted branches or use development credentials; flag activation is separately authorized.
- Logs and artifacts are tenant/environment isolated and contain no secrets, kubeconfig, tokens, or payment data.

## Data and security

- Sensitive data handling: scan source, dependencies, images, IaC, and artifacts; never place secrets in pipeline variables visible to logs.
- Audit requirements: retain commit, approver, artifact digest, deployment, migration, flag, rollback, and policy evidence.
- Retention and deletion: apply artifact/log retention by environment and delete temporary credentials/workspaces after runs.
- Use short-lived federated identity, signed/verified images, pinned actions/tools, least privilege, protected branches, and approval gates.
