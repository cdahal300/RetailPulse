# RetailPulse Dev Container

This container is the standard environment for cloud, PWA, QA, Azure, and MCP development.

## Included

- .NET 10 SDK
- Node.js 22 and npm
- Azure CLI and Bicep
- kubectl and kubelogin through Azure CLI
- Helm
- Git, jq, curl, unzip
- PostgreSQL and Redis clients
- PostgreSQL 16 and Redis 7 Compose services
- VS Code extensions for .NET, Azure, Docker, Bicep, ESLint, Prettier, and Playwright

## Usage

Open the repository in VS Code and choose **Reopen in Container**. The `app` service mounts the repository at `/workspaces/RetailPulse-POS-MVP`. PostgreSQL and Redis are available by service name as `postgres` and `redis`.

The container supports cloud and browser development. Windows POS hardware and certified payment-terminal SDKs remain on the host machine.

Do not put Azure credentials, kubeconfig files, payment credentials, or secrets in this folder. Authenticate interactively with Azure CLI or use a secure developer identity mechanism.
