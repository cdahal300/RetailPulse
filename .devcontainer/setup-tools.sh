#!/usr/bin/env bash
set -euo pipefail

install_helm() {
  if command -v helm >/dev/null 2>&1; then
    return
  fi

  curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
}

install_azure_tools() {
  if command -v az >/dev/null 2>&1; then
    az bicep install --only-show-errors >/dev/null 2>&1 || true
    az aks install-cli --only-show-errors >/dev/null 2>&1 || true
  fi

  install_helm
}

restore_dotnet() {
  if [[ -f RetailPulse.sln ]]; then
    dotnet restore RetailPulse.sln
  fi
}

install_pwa() {
  local pwa_dir="src/Web/RetailPulse.Portal"
  if [[ -f "$pwa_dir/package.json" ]]; then
    npm --prefix "$pwa_dir" install
  fi
}

install_azure_tools
restore_dotnet
install_pwa

echo "RetailPulse development container setup complete."
