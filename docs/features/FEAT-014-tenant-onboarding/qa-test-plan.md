# FEAT-014: QA Test Plan

## Test coverage

- Unit: scope resolution, owner all-store access, manager store assignments, invitation expiry, onboarding state, and command idempotency.
- Integration: PostgreSQL constraints, tenant/store/membership persistence, invitation acceptance, audit, cache cleanup, and cross-tenant denial.
- Contract: onboarding API schemas and versioned tenant/store/membership events.
- PWA: owner signup, create three stores, invite manager, store selector, account switch, logout cleanup, and responsive onboarding.
- Security: tampered tenant/store IDs, expired invitation, wrong issuer/audience, revoked membership, replayed commands, and cross-tenant cache lookup.
- Resilience: identity provider outage, database timeout, duplicate delivery, partial completion, retry, and rollback.

## Required evidence

- One owner creates three stores and sees only those stores.
- A manager assigned to one store is denied access to the other two.
- Two independent tenants cannot read or mutate each other's data.
- Replaying onboarding and invitation commands returns the original result.
- Membership revocation removes access within the documented token/cache window.
- Browser storage contains no invitation token, access token, or cross-account data after logout.
