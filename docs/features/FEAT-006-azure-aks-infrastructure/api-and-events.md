# FEAT-006: API and Event Contracts

## APIs and commands

- Infrastructure interfaces: versioned IaC modules, environment parameters, AKS health endpoints, and operational commands for deploy, scale, backup, restore, and failover.
- No new RetailPulse public business API or domain event is introduced by infrastructure.
- Authentication and authorization: Azure RBAC, Kubernetes RBAC, managed identity, workload identity, and private network controls are required.
- Idempotency behavior: provisioning is declarative and repeatable; operational commands require a change ID and safe re-execution semantics.
- Error model: plan/apply, policy, quota, health, backup, and access failures are surfaced with actionable codes and correlation IDs.

## Events

- No new public domain events are introduced. Azure Activity Logs, deployment records, health metrics, and audit logs are operational telemetry, not business contracts.
- Existing application event envelopes remain unchanged across infrastructure deployment.
- Ownership: platform engineering owns resource and policy contracts; application teams own business APIs/events.

## Compatibility

- Additive-change policy: add optional module inputs, labels, alerts, and compatible resource settings with defaults.
- Breaking-change policy: version modules and environment contracts; require migration, capacity, and rollback plans before changing network, identity, or data resources.
- Contract-test location: IaC validation/policy tests in the infrastructure pipeline; service health/API contracts in `tests/Contract/RetailPulse.ContractTests`.
