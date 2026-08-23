# FEAT-009: Rollout and Operations

## Release

- Feature flag: `pwa.manager.v1` gates routes and installable experience; API authorization remains active independently.
- Safe default: read-only authorized views, no checkout/payment-terminal control, and no unsupported offline mutations.
- Migration strategy: version API responses, IndexedDB/cache schema, manifest, and service worker; keep old client API compatibility through propagation.
- Deployment order: APIs/read models, auth policies, notification infrastructure, PWA assets/service worker, pilot users, then store cohorts.
- Approval gates: PWA owner, security/privacy, accessibility, QA, operations, and business owner approve device matrix.

## Rollout

- Targeting plan: internal users, pilot stores, browser/device cohorts, then broader tenants; keep API and PWA flags separate.
- Metrics: load success, API latency/errors, auth denials, cache age, command pending age, service-worker failures, notification delivery, and accessibility defects.
- Alerts and runbooks: alert on elevated client errors, stale cache, command backlog, auth anomalies, and push failures; link PWA support runbook.
- Expansion criteria: device/browser smoke pass, no cross-store exposure, acceptable performance, and successful offline/reconnect tests.

## Rollback

- First action: disable PWA routes/activation and stop new service-worker promotion while APIs remain backward compatible.
- Data and event handling: preserve server commands and audit records; clear only invalid client cache through a versioned reset.
- Deployment rollback: restore previous static asset/service-worker version and compatible API behavior.
- Recovery validation: verify auth, cache reset, command reconciliation, notifications, and no payment/checkout capability.

## Ownership

- Feature owner: Web/PWA team.
- On-call owner: Web operations with identity/cloud support.
- Expiry or cleanup issue: remove temporary route/cache compatibility and pilot flags after client propagation window.
