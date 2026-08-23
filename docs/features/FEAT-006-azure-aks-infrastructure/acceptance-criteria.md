# FEAT-006: Acceptance Criteria

## Functional behavior

- Given a versioned environment definition, when provisioning runs with approved parameters, then AKS, networking, identity, registry, data, messaging, secrets, and monitoring resources are created consistently.
- Given a workload deployment, when it uses managed/workload identity, then it can access only its approved Azure resources without embedded credentials.
- Given a node or pod failure, when health and autoscaling policies apply, then workloads recover within documented availability targets.

## Failure and resilience behavior

- Given an IaC plan failure, policy violation, quota issue, or partial apply, then deployment stops safely and exposes a recoverable state without destructive retries.
- Given an AKS zone, node pool, database, Service Bus, or region incident, then documented backup, failover, degraded-mode, and recovery procedures preserve business data.
- Given ingress or dependency unavailability, then WAF, health probes, retries, and maintenance behavior prevent unsafe traffic routing.

## Authorization and isolation

- Environment, subscription, resource group, namespace, network, and tenant boundaries are explicit and least privilege.
- Production operators use approved role-based access; workloads use scoped managed identity and network policy; test credentials cannot reach production.
- Private endpoints and firewall rules prevent unintended public access to PostgreSQL, Service Bus, Storage, Key Vault, and App Configuration.

## Data and security

- Sensitive data handling: secrets are stored in Key Vault; no credentials, tokens, card data, or kubeconfig files are in IaC, images, logs, or state artifacts.
- Audit requirements: retain IaC changes, approvals, Azure Activity Logs, policy decisions, access events, and restore tests.
- Retention and deletion: define backup, log, blob, database, and dead-letter retention by environment and data classification.
- Enforce encryption at rest/in transit, WAF, vulnerability baselines, image signing/scanning, resource locks where appropriate, and cost/owner/data-classification tags.
