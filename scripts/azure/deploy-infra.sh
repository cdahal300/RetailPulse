#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 6 ]]; then
  echo "Usage: $0 <subscription-id> <resource-group> <location> <environment> <owner-tag> <postgres-admin-password> [deployment-name]"
  echo "Example: $0 <sub-id> rp-dev-rg eastus dev platform-engineering 'StrongPasswordHere'"
  exit 1
fi

subscription_id="$1"
resource_group="$2"
location="$3"
environment="$4"
owner_tag="$5"
postgres_admin_password="$6"
deployment_name="${7:-feat006-${environment}-$(date +%Y%m%d%H%M%S)}"

if [[ ! -f "infra/environments/${environment}/main.bicepparam" ]]; then
  echo "Unknown environment '${environment}'. Expected one of: dev, test, staging, prod"
  exit 1
fi

az account set --subscription "$subscription_id"

az group create \
  --name "$resource_group" \
  --location "$location" \
  --tags owner="$owner_tag" environment="$environment" workload=retailpulse >/dev/null

echo "Deploying FEAT-006 infrastructure to ${resource_group}..."
az deployment group create \
  --name "$deployment_name" \
  --resource-group "$resource_group" \
  --template-file infra/environments/main.bicep \
  --parameters "infra/environments/${environment}/main.bicepparam" \
  --parameters ownerTag="$owner_tag" postgresAdminPassword="$postgres_admin_password" location="$location"

echo "Deployment complete."
