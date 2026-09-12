#!/usr/bin/env bash
set -euo pipefail

# Helper script to provision Azure OpenAI and deploy gpt-4o-mini for RetailPulse

RESOURCE_GROUP="${1:-rg-retailpulse-dev}"
LOCATION="${2:-eastus}"
ACCOUNT_NAME="${3:-aoai-retailpulse-$RANDOM}"
DEPLOYMENT_NAME="${4:-gpt-4o-mini}"

echo "=== RetailPulse Azure OpenAI Setup ==="
echo "Resource Group:  $RESOURCE_GROUP"
echo "Location:        $LOCATION"
echo "Account Name:    $ACCOUNT_NAME"
echo "Deployment Name: $DEPLOYMENT_NAME"
echo "======================================"

# 1. Register CognitiveServices provider
echo "Ensuring Microsoft.CognitiveServices provider is registered..."
az provider register --namespace Microsoft.CognitiveServices --wait

# 2. Create Resource Group if it doesn't exist
echo "Creating resource group '$RESOURCE_GROUP' in '$LOCATION'..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output table

# 3. Create Azure OpenAI Account
echo "Creating Azure OpenAI account '$ACCOUNT_NAME'..."
az cognitiveservices account create \
  --name "$ACCOUNT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --kind OpenAI \
  --sku S0 \
  --custom-domain "$ACCOUNT_NAME" \
  --output table

# 4. Deploy model (gpt-4o)
echo "Deploying model '$DEPLOYMENT_NAME' (gpt-4o)..."
az cognitiveservices account deployment create \
  --name "$ACCOUNT_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --deployment-name "$DEPLOYMENT_NAME" \
  --model-name gpt-4o \
  --model-version "2024-11-20" \
  --model-format OpenAI \
  --sku-name Standard \
  --sku-capacity 10 \
  --output table

# 5. Retrieve Keys and Endpoint
ENDPOINT=$(az cognitiveservices account show --name "$ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP" --query "properties.endpoint" -o tsv)
API_KEY=$(az cognitiveservices account keys list --name "$ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP" --query "key1" -o tsv)

echo ""
echo "=== Setup Complete! ==="
echo "Add these environment variables to your terminal or local config:"
echo ""
echo "export AZURE_OPENAI_ENDPOINT=\"$ENDPOINT\""
echo "export AZURE_OPENAI_API_KEY=\"$API_KEY\""
echo "export AZURE_OPENAI_DEPLOYMENT=\"$DEPLOYMENT_NAME\""
echo "export AZURE_OPENAI_PROMPT_VERSION=\"azure-openai-v1\""
echo "======================="
