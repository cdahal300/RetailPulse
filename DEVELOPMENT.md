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

## Optional Azure OpenAI development integration

Azure OpenAI is disabled by default. The insights service uses its unavailable/degraded state until all required settings are supplied. To enable it for a local run, set these environment variables before starting the cloud API or Compose:

```bash
export AZURE_OPENAI_ENDPOINT="https://<resource-name>.openai.azure.com"
export AZURE_OPENAI_API_KEY="<local-development-key>"
export AZURE_OPENAI_DEPLOYMENT="<deployment-name>"
export AZURE_OPENAI_PROMPT_VERSION="azure-openai-v1"
```

For direct `dotnet run`, ASP.NET Core also reads the equivalent configuration keys `AzureOpenAI__Endpoint`, `AzureOpenAI__ApiKey`, `AzureOpenAI__Deployment`, and `AzureOpenAI__PromptVersion`. Do not place API keys in checked-in appsettings files; use environment variables, local user secrets, or Azure Key Vault instead.
