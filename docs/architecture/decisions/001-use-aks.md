# Use AKS as the Application Host

* Status: Proposed
* Deciders: TBD
* Date: 2026-08-22

## Context and Problem Statement

RetailPulse needs a repeatable cloud host for APIs, workers, and adapters while keeping checkout independent of cloud availability. The platform also needs managed persistence, messaging, secrets, configuration, and observability. The MVP should preserve modular ownership without prematurely splitting into services.

## Decision Drivers

- DEP-001: Support independent deployment of cloud API and worker workloads.
- OPS-001: Use managed Azure dependencies where they reduce operational ownership.
- ARC-001: Keep a modular monolith first and extract services only when ownership, scale, or release cadence requires it.
- RES-001: Keep the store edge operational when cloud connectivity is unavailable.

## Considered Options

### Option 1: AKS with a modular monolith first

Pros: supports workload identity, controlled environments, ingress, scaling, and later extraction; matches the platform roadmap. Cons: Kubernetes adds operational complexity and requires platform expertise.

### Option 2: Azure App Service or Container Apps

Pros: lower initial platform overhead. Cons: less direct alignment with the planned AKS topology and may require a later hosting migration for workload, networking, or deployment needs.

### Option 3: Self-managed virtual machines

Pros: maximum host control. Cons: higher patching and availability burden with no MVP benefit.

## Decision

Host cloud APIs, workers, and integration workloads on AKS. Deploy a modular monolith first, with explicit module boundaries and interfaces. Use managed Azure PostgreSQL, Service Bus, Blob Storage, Key Vault, App Configuration, Monitor, and Azure OpenAI as separate dependency boundaries. The edge runtime remains a separately deployable local application and is not dependent on AKS for checkout.

## Positive Consequences

- POS-001: Cloud deployment, health checks, scaling, and rollback have a common platform.
- POS-002: Workload identity can avoid application-held Azure credentials.
- POS-003: Module boundaries can mature before service extraction.

## Negative Consequences

- NEG-001: The team must operate Kubernetes configuration, ingress, observability, and upgrades.
- NEG-002: Small workloads may incur platform complexity before scale requires it.
- NEG-003: Network and identity configuration must be designed per environment.

## Implementation Notes

- Define namespaces and workload identities per environment and trust boundary.
- Keep cloud commands idempotent and event handlers retry-safe.
- Document extraction triggers before splitting a module into a service.

## References

- [Architecture overview](../overview.md)
- [Feature roadmap](../../features/ROADMAP.md)
