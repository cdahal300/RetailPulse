# FEAT-009: Acceptance Criteria

## Functional behavior

Current status: the dashboard, offline cache, service-worker, and dev API paths are implemented and verified. Staging identity, iOS, push, and database evidence remains pending.

- Given an authenticated manager or owner, when the PWA loads, then role-aware sales, inventory, sync-health, alerts, settings, and AI views show only authorized data.
- Given a supported desktop, tablet, Android, or iOS browser, when the user navigates and performs an allowed command, then controls remain usable and status distinguishes pending from confirmed.
- Given an offline launch, when cached data exists, then the PWA shows clearly labeled stale read-only data and queues only explicitly supported commands.
- Given an offline manager adjustment, when connectivity returns, then the command retries with its stable client ID and shows confirmed, duplicate, or reviewable status without storing an access token.
- Given notification permission is denied, unavailable, or granted, then the PWA shows the corresponding state; granted notifications deep-link to the relevant alerts view when selected.
- Given a manager or owner opens settings, then only an owner can view or update store configuration and stale updates remain reviewable without overwriting newer settings.
- Given an authorized manager or owner views insights, then the PWA shows advisory source and validation metadata and cannot use insight output to mutate inventory, pricing, payments, or checkout.
- Given a configured VAPID provider, when a manager enables notifications, then the browser subscription is registered for the authenticated tenant, store, and user; without provider configuration, the PWA shows an unavailable state.
- Given a privileged action or revoked identity, then the cloud records the audit context durably and rejects later requests using the persisted tenant-scoped revocation state.

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

## Modern view acceptance additions

- Given a manager opens the PWA, when the overview renders, then freshness, the highest-priority operational issue, and the next safe action are visible without opening another view.
- Given a phone viewport, when the manager navigates, then Overview, Inventory, Alerts, and More remain reachable through touch-sized controls without horizontal scrolling.
- Given an owner opens insights, when an advisory result is displayed, then model, prompt, validation, and source metadata remain visible beside the summary.
- Given cached, partial, reviewable, or unavailable data, when a view renders, then the state is explicit and the UI does not present it as current verified data.
