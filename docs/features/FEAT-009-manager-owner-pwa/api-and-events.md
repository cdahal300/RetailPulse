# FEAT-009: API and Event Contracts

## APIs and commands

- Queries: `GET /api/v1/me`, store-scoped sales/inventory/sync-health/insight endpoints, and notification preferences.
- MVP sales query: `GET /api/v1/tenants/{tenantId}/stores/{storeId}/reports/sales` from FEAT-010. The PWA passes manager-scoped RetailPulse identity headers and displays freshness/source metadata from the response.
- MVP sync-health query: `GET /api/v1/tenants/{tenantId}/stores/{storeId}/sync-health` returns pending, retry, conflict, dead-letter, oldest-pending, and last-success values for an authorized manager or owner scope.
- Commands: authorized manager commands use `POST /api/v1/stores/{storeId}/commands` with command type, payload, client command ID, and expected version.
- Offline command behavior: the PWA may persist only non-sensitive manager command payloads locally, retries with the current authenticated session after reconnect, and removes a command only after a confirmed or duplicate-safe server result; conflicts remain reviewable.
- Notification behavior: production builds register the service worker, support browser permission state and notification deep links, and handle push payloads without storing notification secrets in the browser; provider subscription requires a configured VAPID/public-key contract.
- Owner settings: `GET` and `PUT /api/v1/tenants/{tenantId}/stores/{storeId}/settings` are owner-only, tenant/store scoped, and use an expected version to reject stale updates.
- Insights: `POST` and `GET /api/v1/tenants/{tenantId}/stores/{storeId}/insights` are manager/owner scoped and return advisory, schema-validated results with source, prompt, model, and validation metadata; the current provider is deterministic until Azure OpenAI is configured.
- Authentication and authorization: configurable MSAL browser session for Entra ID plus server-side role/store/tenant policy; PWA never trusts route visibility for authorization. Synthetic manager headers are limited to explicit local `VITE_DEMO_MODE=true`.
- Idempotency behavior: client command ID plus store/user scope deduplicates retries; response includes pending, accepted, confirmed, or reviewable status.
- Error model: stable unauthenticated, forbidden, stale, validation, offline, transient, and reviewable states safe for user display.

## Events

- No new public domain event is required for the PWA. It consumes existing read models and notifications; command side effects publish events owned by their domain.
- Notification delivery may use `LowStockDetected.v1`, `SyncStatusChanged.v1`, and approved operational events.
- Ownership: PWA owns presentation/cache contracts; cloud domains own authorization, command, and event semantics.

The Cloud API validates Entra bearer tokens when `Entra__TenantId` and `Entra__Audience` are configured. Required claims are mapped into the existing RetailPulse tenant/store/role authorization policy; the PWA must not use client-only role checks as an authorization boundary.

## Compatibility

- Additive-change policy: optional response fields and cache records; clients tolerate unknown fields and stale data.
- Breaking-change policy: version API/service-worker/cache schemas and support the previous client during deployment propagation.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; PWA E2E/accessibility suites under `tests/Pwa`.
