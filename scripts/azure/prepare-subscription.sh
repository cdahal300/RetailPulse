#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 4 ]]; then
  echo "Usage: $0 <subscription-id> <location> <prefix> <owner-tag>"
  echo "Example: $0 00000000-0000-0000-0000-000000000000 eastus rp platform-engineering"
  exit 1
fi

subscription_id="$1"
location="$2"
prefix="$3"
owner_tag="$4"

echo "Setting active subscription..."
az account set --subscription "$subscription_id"

echo "Registering required providers..."
providers=(
  Microsoft.ContainerService
  Microsoft.ContainerRegistry
  Microsoft.KeyVault
  Microsoft.Storage
  Microsoft.ServiceBus
  Microsoft.DBforPostgreSQL
  Microsoft.OperationalInsights
  Microsoft.OperationsManagement
  Microsoft.ManagedIdentity
  Microsoft.Network
)
for provider in "${providers[@]}"; do
  az provider register --namespace "$provider" --wait >/dev/null
  echo "  - Registered $provider"
done

echo "Creating management resource group for FEAT-006 (safe to re-run)..."
az group create \
  --name "${prefix}-platform-bootstrap-rg" \
  --location "$location" \
  --tags owner="$owner_tag" workload=retailpulse purpose=platform-bootstrap >/dev/null

echo "Subscription preparation complete."
