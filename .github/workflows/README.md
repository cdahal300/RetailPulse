# GitHub Actions

- `ci.yml`: pull-request and main-branch build, test, PWA lint/build, and secret scan.
- `deploy-aks.yml`: manually triggered, environment-gated AKS deployment. It uses Azure OIDC login, builds immutable Cloud/Edge images, pushes them to ACR, scans the pushed digests, creates runtime Kubernetes secrets from GitHub environment secrets, applies the selected Kustomize overlay, and verifies health.

Production deployment should use GitHub environment protection rules and OIDC federation to Azure. Do not store long-lived Azure credentials in repository secrets.

## Environment Configuration

Configure these GitHub environment variables for each deployment environment:

- `ACR_LOGIN_SERVER`: Azure Container Registry login server, for example `retailpulsedevzhztnpacr.azurecr.io`.
- `AKS_CLUSTER_NAME`: Target AKS cluster name.
- `AZURE_CLIENT_ID`: Federated identity client ID used by `azure/login`.
- `AZURE_LOCATION`: Azure region used when optional infrastructure deployment is enabled.
- `AZURE_RESOURCE_GROUP`: Target resource group.
- `AZURE_SUBSCRIPTION_ID`: Target Azure subscription ID.
- `AZURE_TENANT_ID`: Target Microsoft Entra tenant ID.
- `OWNER_TAG`: Owner tag used when optional infrastructure deployment is enabled.

Configure these GitHub environment secrets:

- `POSTGRES_ADMIN_PASSWORD`: PostgreSQL admin password used only when optional infrastructure deployment is enabled.
- `POSTGRES_CONNECTION_STRING`: Runtime PostgreSQL connection string for the application.
- `REDIS_CONNECTION_STRING`: Optional runtime Redis connection string for the application when Redis is enabled.
