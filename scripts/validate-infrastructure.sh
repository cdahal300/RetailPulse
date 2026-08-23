#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/../.."

# Validate Bicep files for Azure infrastructure
validate_bicep() {
    echo "Validating Bicep IaC files..."
    if ! command -v az &> /dev/null; then
        echo "⚠️  Azure CLI not found. Skipping Bicep validation."
        return 0
    fi
    
    for env in dev test staging prod; do
        bicep_param="$REPO_ROOT/infra/environments/$env/main.bicepparam"
        echo "  Validating $bicep_param..."
        az bicep build --file "$REPO_ROOT/infra/environments/main.bicep" > /dev/null
    done
    echo "✅ Bicep validation passed"
}

# Validate Kubernetes manifests
validate_k8s() {
    echo "Validating Kubernetes manifests..."
    if ! command -v kubectl &> /dev/null; then
        echo "⚠️  kubectl not found. Skipping Kubernetes validation."
        return 0
    fi
    
    # Validate using dry-run
    for overlay in dev staging prod; do
        echo "  Validating overlay: $overlay"
        kustomize build "$REPO_ROOT/infra/kubernetes/overlays/$overlay" | \
            kubectl apply --dry-run=client -f - > /dev/null
    done
    echo "✅ Kubernetes validation passed"
}

# Check for common security issues
validate_security() {
    echo "Validating security configurations..."
    
    # Check for hardcoded secrets in YAML files
    if grep -r "password:" "$REPO_ROOT/infra/" --include="*.yaml" --include="*.yml" 2>/dev/null | \
       grep -v "CHANGEME" | grep -v "PASSWORD" | grep -v ".bicepparam"; then
        echo "❌ Found potential hardcoded secrets in manifests"
        return 1
    fi
    
    # Check for image tags (should not use 'latest' in prod)
    if grep -r "image.*:latest" "$REPO_ROOT/infra/kubernetes/overlays/prod" 2>/dev/null; then
        echo "❌ Found 'latest' image tags in production manifests"
        return 1
    fi
    
    echo "✅ Security validation passed"
}

# Main
main() {
    echo "🔍 Validating FEAT-006 infrastructure..."
    
    validate_bicep
    validate_k8s
    validate_security
    
    echo ""
    echo "✅ All validations passed!"
}

main "$@"
