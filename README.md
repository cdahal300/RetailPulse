# RetailPulse POS MVP

Offline-first, multi-tenant retail point-of-sale SaaS platform with Azure cloud services and optional open-source components.

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

The repository currently contains the architecture baseline in `docs/architecture/overview.md`. Implementation should start with the local edge checkout slice, then synchronization, then cloud persistence and dashboards.

## Engineering principles

1. The store edge can complete a sale without an internet connection.
2. Every synchronized command is idempotent.
3. Payment processing remains external; RetailPulse stores references, never card data.
4. AI is asynchronous, advisory, governed, and outside the checkout critical path.
5. Business events and API contracts are versioned.
6. Tests and observability are built with each vertical slice.

The product serves multiple independent retail businesses. Tenant and store isolation is required across every application, data, event, cache, analytics, PWA, feature-flag, and AI boundary.

## Planned stack

- C# and .NET for edge, API, workers, and integration adapters
- TypeScript and React for manager and owner web experiences
- Responsive PWA for manager and owner mobile access; native mobile is deferred unless device capabilities require it
- SQLite at the edge and PostgreSQL in the cloud
- Azure Kubernetes Service, Service Bus, Blob Storage, Key Vault, and Monitor
- Azure OpenAI in production with Ollama available for local AI development
