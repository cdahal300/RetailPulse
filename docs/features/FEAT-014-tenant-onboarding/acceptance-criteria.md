# FEAT-014: Acceptance Criteria

## Functional behavior

- Given a new customer, when the owner completes onboarding, then one internal RetailPulse tenant is created and the owner is assigned to it.
- Given an owner creates locations, when each store is saved, then it receives a stable store ID, tenant scope, timezone, currency, and configuration version.
- Given an owner invites a manager, when the invitation is accepted, then the manager receives only the assigned store memberships.
- Given an owner opens the PWA, when the store selector loads, then it contains only stores authorized for the current tenant and user.
- Given an owner has three stores, when they switch stores, then reports, alerts, sync status, insights, and caches use the selected authorized store scope.

## Isolation and authorization

- Given two businesses in one deployment, when Tenant A requests Tenant B data, then the API returns a safe denial and does not reveal whether the record exists.
- Given a manager assigned to Store A, when they request Store B, then the API denies the request even if the client changes the route or request body.
- Given an owner without a store restriction, when they request any store in their tenant, then the request is allowed; requests outside the tenant remain denied.
- Given a device registration, when it is persisted, then the device has exactly one tenant and store scope and cannot carry user roles.
- Given a logout or account switch, when the PWA clears local state, then caches and queued commands from the previous scope are not visible to the next account.

## Failure and resilience

- Given a duplicate onboarding or invitation command, when it is retried with the same idempotency key, then no duplicate tenant, store, or membership is created.
- Given an expired or revoked invitation, when it is accepted, then it is rejected without creating a membership.
- Given identity-provider or database outage, when onboarding is submitted, then the UI shows retryable failure and does not mark the step complete.
- Given partial onboarding, when the owner returns, then completed steps are visible and incomplete steps can resume safely.

## Security and audit

- Every tenant and store mutation records actor, tenant, store where applicable, correlation ID, reason, timestamp, and result.
- Cross-tenant and cross-store denial tests cover reads, writes, events, caches, exports, analytics, and insight requests.
- No invitation token, access token, secret, payment data, or raw customer data is stored in browser local storage.
