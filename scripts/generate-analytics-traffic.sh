#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-http://localhost:5000}"
requests="${2:-20}"
tenant_id="${RETAILPULSE_TENANT_ID:-tenant-1}"
store_ids="${RETAILPULSE_STORE_IDS:-store-1,store-2}"
subject_id="${RETAILPULSE_SUBJECT_ID:-manager-traffic}"

issued_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
expires_at="$(date -u -d '+1 hour' +%Y-%m-%dT%H:%M:%SZ)"

IFS=',' read -r -a stores <<< "$store_ids"

for index in $(seq 1 "$requests"); do
  store_id="${stores[$(((index - 1) % ${#stores[@]}))]}"
  correlation_id="analytics-traffic-${index}"
  url="${base_url%/}/api/v1/tenants/${tenant_id}/stores/${store_id}/reports/sales?from=2026-08-23T00:00:00Z&to=2026-08-24T00:00:00Z&timezone=UTC&currency=USD"

  status=$(curl -sS -o /tmp/retailpulse-analytics-response.json -w '%{http_code}' \
    -H "X-RetailPulse-Token-Id: analytics-token-${index}" \
    -H "X-RetailPulse-Subject-Id: ${subject_id}" \
    -H "X-RetailPulse-Tenant-Id: ${tenant_id}" \
    -H "X-RetailPulse-Store-Id: ${store_id}" \
    -H "X-RetailPulse-Principal-Type: User" \
    -H "X-RetailPulse-Roles: Manager" \
    -H "X-RetailPulse-Issued-At: ${issued_at}" \
    -H "X-RetailPulse-Expires-At: ${expires_at}" \
    -H "X-Correlation-Id: ${correlation_id}" \
    "$url")

  if [[ "$status" != "200" ]]; then
    echo "request ${index} failed with HTTP ${status}"
    cat /tmp/retailpulse-analytics-response.json
    exit 1
  fi

  net_sales=$(jq -r '.summary.netSalesMinor' /tmp/retailpulse-analytics-response.json)
  orders=$(jq -r '.summary.orderCount' /tmp/retailpulse-analytics-response.json)
  echo "request=${index} store=${store_id} status=${status} netSalesMinor=${net_sales} orders=${orders}"
done