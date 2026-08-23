# FEAT-006: Rollout and Operations

## Release

- Feature flag: no application feature flag; infrastructure changes use versioned IaC and approved change records.
- Safe default: private networking, least privilege, deny-by-default policies, and non-production resource sizes until capacity is proven.
- Migration strategy: provision new resources additively, validate backups and data migration, then cut traffic; avoid in-place destructive changes.
- Deployment order: subscriptions/policies, network/private endpoints, identity/Key Vault, registry, data/messaging, AKS/ingress, monitoring, workloads.
- Approval gates: platform owner, security, finance, data owner, operations, and production change approval.

## Rollout

- Targeting plan: development, test, staging, production with environment-specific parameters and no credential reuse.
- Metrics: pod/node health, capacity, API availability, ingress errors, database/Service Bus health, backup success, security policy violations, and cost.
- Alerts and runbooks: link AKS upgrade, secret rotation, backup/restore, node failure, WAF, and regional recovery runbooks.
- Expansion criteria: policy-clean plan, successful restore, health probes, private connectivity, cost budget, and on-call rehearsal.

## Rollback

- First action: stop promotion and isolate the changed resource or traffic route.
- Data and event handling: preserve databases, blobs, queues, and audit logs; do not destroy state to undo an infrastructure change.
- Deployment rollback: revert IaC/module version where safe; for irreversible changes, use forward repair or restore a validated backup.
- Recovery validation: verify identities, network boundaries, health, backups, data integrity, and application connectivity.

## Ownership

- Feature owner: Azure platform engineering.
- On-call owner: Cloud infrastructure operations.
- Expiry or cleanup issue: remove temporary resources, exceptions, and elevated permissions after each environment reaches steady state.
