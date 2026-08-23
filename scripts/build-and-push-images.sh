#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <registry-name> <image-tag> [--push]"
  echo "Example: $0 retailpulseacr.azurecr.io v1.0.0 --push"
  exit 1
fi

registry="$1"
tag="$2"
push_images="${3:---no-push}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."

echo "🐳 Building RetailPulse Docker images..."

# Ensure registry name doesn't have trailing slash
registry="${registry%/}"

# Build Cloud API
echo "  Building Cloud API..."
docker build \
  -f "$REPO_ROOT/src/Cloud/RetailPulse.Cloud/Dockerfile" \
  -t "$registry/retailpulse-cloud:$tag" \
  -t "$registry/retailpulse-cloud:latest" \
  "$REPO_ROOT"

# Build Edge API
echo "  Building Edge API..."
docker build \
  -f "$REPO_ROOT/src/Edge/RetailPulse.Edge/Dockerfile" \
  -t "$registry/retailpulse-edge:$tag" \
  -t "$registry/retailpulse-edge:latest" \
  "$REPO_ROOT"

echo "✅ Build complete"

if [[ "$push_images" == "--push" ]]; then
  echo "📤 Pushing images to registry..."
  
  if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install it to push images."
    exit 1
  fi
  
  # Login to ACR if needed
  registry_name="${registry%%.azurecr.io*}"
  echo "  Logging in to $registry_name..."
  az acr login --name "$registry_name"
  
  # Push images
  echo "  Pushing Cloud API..."
  docker push "$registry/retailpulse-cloud:$tag"
  docker push "$registry/retailpulse-cloud:latest"
  
  echo "  Pushing Edge API..."
  docker push "$registry/retailpulse-edge:$tag"
  docker push "$registry/retailpulse-edge:latest"
  
  echo "✅ Push complete"
  echo ""
  echo "Images published:"
  echo "  $registry/retailpulse-cloud:$tag"
  echo "  $registry/retailpulse-cloud:latest"
  echo "  $registry/retailpulse-edge:$tag"
  echo "  $registry/retailpulse-edge:latest"
else
  echo ""
  echo "To push images to registry, run:"
  echo "  $0 $registry $tag --push"
fi
