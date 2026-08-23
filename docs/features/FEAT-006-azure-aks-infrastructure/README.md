# FEAT-006: Azure Platform and AKS Infrastructure

## Outcome

As an engineering team, we want repeatable Azure environments so that RetailPulse can be deployed consistently and recovered safely.

## Current MVP status

Azure deployment is deferred for the current sponsorship subscription. The subscription rejected managed PostgreSQL provisioning and Azure Container Registry during validation. Development continues in the checked-in Dev Container, using local PostgreSQL 16 and Redis 7 from Docker Compose. The Azure composition remains a future deployment target and must be revalidated against supported subscription capabilities before use.

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

Foundation for FEAT-007 and all cloud features. For the current MVP, validate application behavior locally in the Dev Container first; defer Azure integration, policy, private networking, and disaster-recovery tests until supported services are available.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
