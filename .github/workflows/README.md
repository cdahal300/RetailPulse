# GitHub Actions

- `ci.yml`: pull-request and main-branch build, test, PWA lint/build, and secret scan.
- `deploy-aks.yml`: manually triggered deployment gate. It remains blocked until FEAT-006 provides infrastructure and deployment manifests, then FEAT-007 can add federated Azure login, ACR verification, and AKS rollout steps.

Production deployment should use GitHub environment protection rules and OIDC federation to Azure. Do not store long-lived Azure credentials in repository secrets.
