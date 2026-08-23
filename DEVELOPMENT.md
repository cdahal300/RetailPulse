# Development Setup

## Dev Container

Use the checked-in Dev Container for the cloud API, PWA, tests, Azure tooling, and MCP development. Open the repository in VS Code and choose **Reopen in Container**. The container starts PostgreSQL and Redis through Compose.

## Host-only work

Run hardware-dependent POS work on the Windows host. This includes scanner, printer, cash drawer, and certified payment-terminal integration. The local edge service can still connect to the containerized databases when network access is configured.

## Local services

| Service | Container hostname | Port |
|---|---|---:|
| PostgreSQL | `postgres` | 5432 |
| Redis | `redis` | 6379 |
| Cloud API | `localhost` | 5000 |
| PWA | `localhost` | 5173 |

Do not commit credentials. The Compose password is for local development only.
