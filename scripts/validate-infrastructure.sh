#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."

# Validate Bicep files for Azure infrastructure
validate_bicep() {
    echo "Validating Bicep IaC files..."
    if ! command -v az &> /dev/null; then
        echo "⚠️  Azure CLI not found. Skipping Bicep validation."
        return 0
    fi
    
    bicep_output="$(mktemp)"
    az bicep build --file "$REPO_ROOT/infra/environments/main.bicep" --outfile "$bicep_output" > /dev/null
    rm -f "$bicep_output"
    echo "✅ Bicep validation passed"
}

# Validate Kubernetes manifests
validate_k8s() {
    echo "Validating Kubernetes manifests..."
    if command -v kustomize &> /dev/null; then
        kustomize_command=(kustomize build)
    elif command -v kubectl &> /dev/null; then
        kustomize_command=(kubectl kustomize)
    else
        echo "⚠️  kustomize or kubectl not found. Skipping Kubernetes validation."
        return 0
    fi
    
    for overlay in dev staging prod; do
        echo "  Validating overlay: $overlay"
        "${kustomize_command[@]}" "$REPO_ROOT/infra/kubernetes/overlays/$overlay" > /dev/null
    done
    echo "✅ Kubernetes validation passed"
}

# Check for common security issues
validate_security() {
    echo "Validating security configurations..."
    
    if grep -R "kind: Secret\|Password=CHANGEME" "$REPO_ROOT/infra/kubernetes" --include="*.yaml" --include="*.yml"; then
        echo "❌ Found inline Kubernetes secrets or placeholder passwords in manifests"
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
