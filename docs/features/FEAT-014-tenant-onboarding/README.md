# FEAT-014: Tenant Onboarding and Multi-Store Operations

## Status

Proposed

## User outcome

As a business owner, I want to create my retail business, add multiple locations, and invite staff so that each customer operates in an isolated workspace without seeing another business's data.

## Scope

- Create an internal RetailPulse tenant for each customer business.
- Create and manage stores/locations inside that tenant.
- Invite owners and managers through Entra External ID or a supported multi-tenant identity flow.
- Assign owners to the tenant and managers to one or more stores.
- Register POS devices to exactly one tenant and store.
- Provide an owner onboarding checklist and authorized store selector in the PWA.
- Persist onboarding progress and audit every privileged membership or store change.

## Non-goals

- Billing, subscription plans, tax registration, or payment-provider settlement.
- Cross-tenant reporting or benchmarking.
- Self-service deletion without retention and support review.
- Allowing the browser to choose or grant tenant scope.

## Dependencies

- FEAT-005 Identity and role authorization
- FEAT-009 Manager and owner PWA
- FEAT-004 Catalog and inventory management
- Related ADR: [multi-tenant and store isolation](../../architecture/decisions/005-multi-tenant-isolation.md)
- Related checklist: [tenant isolation checklist](../../planning/tenant-isolation-checklist.md)
- External provider: Entra External ID or multi-tenant Entra application configuration

## Architecture impact

- Owning boundary: Cloud identity/onboarding, Cloud tenant/store management, and PWA owner experience.
- Identity model: separate the internal `RetailPulseTenantId` from the external identity tenant, issuer, and subject ID.
- Scope model: an owner with no store restriction can access all stores in their internal tenant; managers and devices receive explicit store assignments.
- Offline behavior: onboarding, invitations, membership changes, and store creation require an authenticated online connection. Existing supported offline reads and queued manager commands remain unchanged.
- Payment boundary impact: None.
- Data model: add tenant, store, membership, invitation, identity-link, and onboarding-state records with tenant-scoped unique keys.
- Events: publish versioned tenant, store, invitation, membership, and device-registration events with tenant scope.
- Feature flag: Required; default off until identity and isolation evidence is complete.

## Onboarding sequence

1. A prospect signs up or accepts an owner invitation.
2. RetailPulse creates one internal tenant and assigns the first owner.
3. The owner creates one or more stores with timezone, currency, and display settings.
4. The owner invites managers and assigns each manager to one or more stores.
5. The owner registers POS devices to a single store.
6. The owner completes catalog, notification, and integration setup.
7. The system runs a tenant isolation and authorization check before activation.

## Development scenarios

Use [`scripts/seed-tenant-scenarios.sh`](../../../scripts/seed-tenant-scenarios.sh) against a Development Cloud API to seed repeatable sales events for mockups and isolation validation:

```bash
./scripts/seed-tenant-scenarios.sh owner-three-stores
./scripts/seed-tenant-scenarios.sh manager-single-store
./scripts/seed-tenant-scenarios.sh second-tenant
```

The scenarios represent one owner with three locations, a manager restricted to one location, and an independent second customer tenant. They seed development analytics events only; they do not create production tenants, invitations, memberships, or devices.

## Acceptance criteria

- Given a new owner, when onboarding completes, then exactly one internal tenant and one owner membership are created idempotently.
- Given an owner with three stores, when the PWA loads, then only those three authorized stores are available for selection and reporting.
- Given two independent businesses, when either owner requests store, report, cache, event, or insight data, then the other tenant's data is denied and absent from the response.
- Given a manager invitation, when the manager accepts it, then the manager can access only the assigned store set and cannot configure tenant-wide ownership.
- Given a device registration, when it is completed, then the device is bound to exactly one tenant and store and cannot impersonate a user.
- Given duplicate onboarding, store, invitation, or membership commands, when retried with the same idempotency key, then the original result is returned without duplicate records.
- Given an unauthorized tenant or store selector, when a request is sent, then server-side authorization denies it regardless of client parameters.
- Given an onboarding dependency outage, when setup is attempted, then no partial privileged state is presented as complete and the owner receives a retryable status.

## QA coverage

- Unit tests: tenant/store scope, membership roles, owner all-store scope, manager assignment scope, onboarding state transitions, and idempotency.
- Integration tests: PostgreSQL tenant/store/membership constraints, invitation acceptance, cross-tenant denial, cache isolation, and audit records.
- Contract tests: onboarding commands and `TenantCreated.v1`, `StoreCreated.v1`, `MembershipChanged.v1`, and invitation event schemas.
- End-to-end tests: owner signup, create three stores, invite manager, switch stores, denied cross-tenant access, and device registration.
- PWA/device coverage: owner onboarding on desktop/tablet/phone, store selector scope, reload/logout behavior, and no horizontal overflow.
- Resilience coverage: duplicate invites, expired invitations, replay, identity-provider outage, and partial onboarding recovery.

## Rollout

- Flag key: `tenant-onboarding.v1`
- Default state: Off
- Targeting plan: development, staging, internal tenant, then one pilot business before wider activation.
- Metrics and alerts: onboarding completion, invitation acceptance, store creation failures, authorization denials, duplicate commands, and cross-tenant security alerts.
- Rollback action: disable the flag, stop new onboarding commands, preserve existing tenant data, and keep existing authorized tenants readable.
- Flag removal issue: remove after billing, support recovery, retention, and multi-tenant isolation evidence are accepted.

## Delivery links

- Pull request: TBD
- Release: TBD
- Post-release validation: tenant isolation report, role matrix, onboarding completion evidence, and audit sample.
