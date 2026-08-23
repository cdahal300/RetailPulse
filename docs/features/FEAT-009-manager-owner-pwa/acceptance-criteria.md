# FEAT-009: Acceptance Criteria

## Functional behavior

- Given an authenticated manager or owner, when the PWA loads, then role-aware sales, inventory, sync-health, alerts, settings, and AI views show only authorized data.
- Given a supported desktop, tablet, Android, or iOS browser, when the user navigates and performs an allowed command, then controls remain usable and status distinguishes pending from confirmed.
- Given an offline launch, when cached data exists, then the PWA shows clearly labeled stale read-only data and queues only explicitly supported commands.

## Failure and resilience behavior

- Given API timeout, offline mode, push failure, or service-worker update failure, then the PWA remains usable for supported cached reads, shows actionable status, and does not invent fresh values.
- Given a duplicate command or reconnect, then the server idempotency result is displayed once and pending status converges to confirmed or reviewable.
- Given stale cache or incompatible service worker, then the PWA prompts/reloads safely without losing approved pending command state.

## Authorization and isolation

- Enforce role, tenant, and store authorization on APIs; UI route guards are supplementary only.
- Cashiers cannot access manager/owner views; managers cannot issue owner-only settings commands; PWA cannot perform checkout or control payment terminals.
- Browser cache and notifications are scoped to the signed-in user/store and clear on logout or account change.

## Data and security

- Sensitive data handling: browser storage contains only minimum non-sensitive read cache and opaque references; never PAN, CVV, PIN, raw card data, tokens beyond secure session needs, or secrets.
- Audit requirements: record sign-in, command submission/result, notification preference changes, and privileged actions server-side.
- Retention and deletion: bound IndexedDB/cache retention and clear user/store data on logout, expiry, or documented reset.
- Use secure cookies/token handling, CSP, dependency scanning, XSS/CSRF protections, service-worker scope controls, and accessible error states.
