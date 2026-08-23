# FEAT-006: Azure Platform and AKS Infrastructure

## Outcome

As an engineering team, we want repeatable Azure environments so that RetailPulse can be deployed consistently and recovered safely.

## Scope

- AKS cluster, node pools, managed identity, workload identity, ingress, and network policy.
- Azure Container Registry, Front Door or Application Gateway with WAF.
- PostgreSQL Flexible Server, Service Bus, Blob Storage, Key Vault, App Configuration, and monitoring.
- Development, test, staging, and production environment composition using IaC.
- Backup, retention, private networking, and tagging policies.

## Acceptance criteria

- A new environment can be provisioned from versioned infrastructure code.
- Application workloads use managed identity rather than embedded credentials.
- Secrets are stored in Key Vault and are never committed.
- AKS health, upgrade, backup, and rollback procedures are documented.
- Production resources have cost, owner, environment, and data-classification tags.

## Dependencies and QA

Foundation for FEAT-007 and all cloud features. Validate IaC, policy checks, least privilege, network boundaries, disaster recovery, and cost limits.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
