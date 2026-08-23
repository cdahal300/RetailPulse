# RetailPulse POS MVP

> A multi-tenant retail operations platform for independent and growing US retailers.

[![CI](https://github.com/cdahal300/RetailPulse/actions/workflows/ci.yml/badge.svg)](https://github.com/cdahal300/RetailPulse/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/PWA-React%20%2B%20TypeScript-149ECA?logo=react&logoColor=white)](src/Web/RetailPulse.Portal/README.md)
[![Azure](https://img.shields.io/badge/cloud-Azure%20%2B%20AKS-0078D4?logo=microsoftazure&logoColor=white)](docs/architecture/decisions/001-use-aks.md)
[![Security](https://img.shields.io/badge/security-tenant%20isolated-15803D)](docs/architecture/decisions/005-multi-tenant-isolation.md)

RetailPulse combines reliable store checkout, inventory visibility, operational health, analytics, and advisory AI in one tenant-isolated SaaS platform. Payment processing remains external through a certified terminal and selected provider.

## Product promise

```text
The POS that tells retail operators what is happening,
what needs attention, and why.
```

## Repository map

- `.github/`: GitHub and Copilot instructions, prompts, skills, agents, and CI workflows
- `.vscode/`: shared editor settings and MCP configuration template
- `docs/`: architecture, decisions, API/event contracts, and delivery runbooks
- `src/`: production code, organized by deployable host and business module
- `tests/`: unit, integration, contract, and end-to-end tests
- `infra/`: infrastructure-as-code and environment configuration
- `scripts/`: local development, validation, and test utilities

## Documentation navigation

- [Architecture documentation](docs/architecture/README.md)
- [Architecture overview](docs/architecture/overview.md)
- [Architecture diagrams](docs/architecture/diagrams/README.md)
- [Architecture decisions](docs/architecture/decisions/README.md)
- [Product scope and personas](docs/product/README.md)
- [Planning and delivery controls](docs/planning/README.md)
- [Feature roadmap](docs/features/ROADMAP.md)

## Current status

The repository contains the architecture baseline, complete feature planning package, buildable .NET/PWA scaffold, and the first checkout domain slice. SQLite persistence, cloud synchronization, Azure infrastructure, CI/CD deployment, OpenTelemetry, analytics, payment-provider certification, PWA workflows, and AI remain staged implementation work.

| Area | Status |
|---|---|
| Architecture and product scope | Defined |
| Multi-tenant isolation model | Defined; implementation enforced incrementally |
| .NET solution and PWA scaffold | Buildable |
| Local checkout domain | Implemented with test doubles |
| SQLite persistence | Planned: FEAT-002 |
| Azure/AKS infrastructure | Planned: FEAT-006 |
| GitHub CI | Configured |
| AKS CD | Gated until infrastructure manifests exist |
| Analytics and AI | Planned: FEAT-010 and FEAT-013 |

## Engineering principles

1. The store edge preserves checkout state and recovers safely when the cloud is unavailable; payment approval remains provider-dependent.
2. Every synchronized command is idempotent.
3. Payment processing remains external; RetailPulse stores references, never card data.
4. AI is asynchronous, advisory, governed, and outside the checkout critical path.
5. Business events and API contracts are versioned.
6. Tests and observability are built with each vertical slice.

The product serves multiple independent retail businesses. Tenant and store isolation is required across every application, data, event, cache, analytics, PWA, feature-flag, and AI boundary.

## Planned stack


## Start here

1. Open the repository in VS Code and choose **Dev Containers: Reopen in Container**.
2. Read the [MVP scope](docs/product/MVP-scope.md) and [feature roadmap](docs/features/ROADMAP.md).
3. Review the [branching strategy](docs/planning/branching-strategy.md).
4. Start implementation with [FEAT-002 Durable SQLite Edge Persistence](docs/features/FEAT-002-sqlite-edge-persistence/README.md).

Local build commands:

```bash
dotnet restore RetailPulse.sln
dotnet build RetailPulse.sln --no-restore
dotnet test RetailPulse.sln --no-build
npm --prefix src/Web/RetailPulse.Portal ci
npm --prefix src/Web/RetailPulse.Portal run lint
npm --prefix src/Web/RetailPulse.Portal run build
```

## Repository status

The repository is hosted at [github.com/cdahal300/RetailPulse](https://github.com/cdahal300/RetailPulse). Pull requests run the CI workflow. Production deployment is intentionally gated until the AKS infrastructure and deployment manifests are implemented.
