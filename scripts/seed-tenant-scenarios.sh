#!/usr/bin/env bash
set -euo pipefail

api_base_url="${RETAILPULSE_API_URL:-http://localhost:5019}"
scenario="${1:-owner-three-stores}"
run_id="$(date +%Y%m%d%H%M%S)"

seed_sale() {
  local tenant_id="$1"
  local store_id="$2"
  local suffix="$3"
  local total_minor="$4"
  local product_id="$5"
  local quantity="$6"
  local event_id="scenario-${scenario}-${run_id}-${suffix}"

  curl --fail-with-body --silent --show-error \
    -X POST "${api_base_url%/}/api/v1/dev/analytics/seed-sale" \
    -H 'Content-Type: application/json' \
    -H "X-RetailPulse-Token-Id: scenario-${run_id}" \
    -H "X-RetailPulse-Subject-Id: scenario-owner" \
    -H "X-RetailPulse-Tenant-Id: ${tenant_id}" \
    -H "X-RetailPulse-Store-Id: ${store_id}" \
    -H 'X-RetailPulse-Principal-Type: User' \
    -H 'X-RetailPulse-Roles: Owner' \
    -H 'X-RetailPulse-Issued-At: 2026-01-01T00:00:00Z' \
    -H 'X-RetailPulse-Expires-At: 2099-01-01T00:00:00Z' \
    -d "$(printf '{\"TenantId\":\"%s\",\"StoreId\":\"%s\",\"EventId\":\"%s\",\"SaleId\":\"scenario-sale-%s\",\"Currency\":\"USD\",\"TotalMinor\":%s,\"OccurredAt\":\"2026-09-12T%sZ\",\"InventoryMovements\":[{\"ProductId\":\"%s\",\"QuantityDelta\":%s}]}' "$tenant_id" "$store_id" "$event_id" "$suffix" "$total_minor" "12:00:00" "$product_id" "$quantity")" \
    | jq -c '{accepted, eventId, source}'
}

case "$scenario" in
  owner-three-stores)
    echo "Seeding owner-three-stores: tenant-demo-acme with three locations..."
    seed_sale tenant-demo-acme store-downtown downtown 5750 sandwich 3
    seed_sale tenant-demo-acme store-airport airport 8200 coffee 5
    seed_sale tenant-demo-acme store-mall mall 4300 tea 4
    ;;
  manager-single-store)
    echo "Seeding manager-single-store: one manager-scoped location..."
    seed_sale tenant-demo-acme store-downtown manager 5750 sandwich 3
    ;;
  second-tenant)
    echo "Seeding second-tenant: an independent customer boundary..."
    seed_sale tenant-demo-green store-market green 9100 coffee 6
    ;;
  *)
    echo "Unknown scenario '$scenario'."
    echo "Expected: owner-three-stores, manager-single-store, second-tenant"
    exit 1
    ;;
esac

echo "Scenario '$scenario' seeded against $api_base_url"
