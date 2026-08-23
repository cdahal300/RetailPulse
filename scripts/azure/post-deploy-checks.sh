#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 ]]; then
  echo "Usage: $0 <subscription-id> <resource-group> <aks-name>"
  exit 1
fi

subscription_id="$1"
resource_group="$2"
aks_name="$3"

az account set --subscription "$subscription_id"

echo "Checking AKS provisioning state..."
az aks show --resource-group "$resource_group" --name "$aks_name" --query "{name:name, provisioningState:provisioningState, kubernetesVersion:kubernetesVersion}" -o table

echo "Checking node pools..."
az aks nodepool list --resource-group "$resource_group" --cluster-name "$aks_name" --query "[].{name:name, count:count, vmSize:vmSize, mode:mode, state:provisioningState}" -o table

echo "Checking non-compliant policy states in resource group..."
az policy state list --resource-group "$resource_group" --query "[?complianceState=='NonCompliant'].{resourceId:resourceId, policyAssignment:policyAssignmentName, definition:policyDefinitionName}" -o table

echo "Checks complete."
