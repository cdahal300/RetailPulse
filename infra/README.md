# Infrastructure layout

- `modules/`: reusable infrastructure modules
- `environments/`: development, test, staging, and production composition
- `policies/`: Azure policy and security configuration
- `README.md`: provisioning prerequisites and deployment sequence

Use infrastructure as code for Azure resources. Never commit credentials, connection strings, or provider secrets.

## FEAT-006 implementation status

Current environment composition is in `infra/environments/main.bicep` with environment-specific parameter files:

- `infra/environments/dev/main.bicepparam`
- `infra/environments/test/main.bicepparam`
- `infra/environments/staging/main.bicepparam`
- `infra/environments/prod/main.bicepparam`

The baseline provisions:

- VNet, subnets, NSG
- AKS with OIDC issuer and workload identity enabled
- Optional separate AKS user node pool for application workloads
- Azure Container Registry
- Key Vault with RBAC and public network disabled
- Service Bus namespace
- Storage account with public access disabled
- App Configuration store
- Log Analytics workspace
- PostgreSQL Flexible Server with delegated subnet and private DNS zone
- Managed identity for workload access patterns

## MVP cost profile guidance

For the current MVP, use the Dev Container and local Docker services first. Do not provision the FEAT-006 Azure composition until the target subscription supports the required managed services.

If Azure infrastructure is needed for a later environment, default to cost-optimized settings in dev/test/staging:

- `mvpCostProfile = true`
- AKS system and user pools on smaller VM sizes with low node count
- One AKS system node only in dev/test; user pools are enabled for staging/prod
- ACR `Standard` tier, the lowest tier accepted by the target sponsorship subscription
- Minimum supported Log Analytics retention
- PostgreSQL burstable minimal SKU and lower storage size
- App Configuration disabled unless required

Production can opt out with `mvpCostProfile = false` when resilience/performance requirements justify cost.

## Prerequisites

1. Azure CLI installed and authenticated (`az login`).
1. Contributor access (or equivalent delegated rights) to target subscription/resource group.
1. Permission to register resource providers.
1. Secure secret input process for PostgreSQL admin password.

## Azure setup instructions (deferred for current sponsorship subscription)

The current sponsorship subscription rejected Azure Database for PostgreSQL provisioning and Azure Container Registry during deployment validation. The commands below remain the repeatable infrastructure path for a subscription that supports those services; they are not required for local MVP development.

1. Prepare subscription and register providers:

```bash
./scripts/azure/prepare-subscription.sh <subscription-id> <location> <prefix> <owner-tag>
```

Example:

```bash
./scripts/azure/prepare-subscription.sh 00000000-0000-0000-0000-000000000000 eastus rp platform-engineering
```

1. Run a what-if deployment preview:

```bash
./scripts/azure/whatif-infra.sh <subscription-id> <resource-group> <location> <environment> <owner-tag> <postgres-admin-password>
```

1. Deploy an environment:

```bash
./scripts/azure/deploy-infra.sh <subscription-id> <resource-group> <location> <environment> <owner-tag> <postgres-admin-password>
```

1. Run post-deploy checks against the same subscription:

```bash
./scripts/azure/post-deploy-checks.sh <subscription-id> <resource-group> <aks-name>
```

## Safety and operations notes

- Run `what-if` before every apply in staging and production.
- For MVP, start with `dev` only, validate workload behavior, then promote to `test`.
- Do not commit parameter files containing secrets.
- Keep `postgresAdminPassword` in secure CI/CD secret stores.
- Use environment-specific resource groups and never reuse production credentials in lower environments.
- Environments may share one subscription or use different subscriptions; pass the intended subscription ID on every command, for example `dev -> <subscription-id>` and `prod -> <subscription-id>`.
- Each script sets the active Azure subscription explicitly; do not rely on the CLI's previously selected subscription.
- Add policy assignments in `infra/policies/` before production rollout.
