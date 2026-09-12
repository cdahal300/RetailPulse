# FEAT-014: Rollout and Operations

## Deployment order

1. Complete internal tenant, store, membership, and invitation migrations.
2. Deploy APIs and consumers with `tenant-onboarding.v1` disabled.
3. Run cross-tenant, cross-store, idempotency, and audit checks in development and staging.
4. Enable onboarding for an internal test tenant.
5. Enable one pilot business with multiple stores.
6. Review authorization denials, invitation completion, onboarding failures, and cache isolation.
7. Expand gradually after support, retention, and recovery procedures are accepted.

## Rollback

Disable `tenant-onboarding.v1` to stop new onboarding and membership changes. Do not delete tenant data during rollback. Existing tenant reads and existing authorized operations remain available while the incident is investigated.

## Operational requirements

- Alert on cross-tenant authorization denials, repeated invitation failures, membership version conflicts, and unexpected store-scope changes.
- Audit all tenant creation, store creation, invitation, membership, device registration, and revocation actions.
- Provide support procedures for expired invitations, wrong store assignments, account recovery, and tenant deletion requests.
- Keep external identity tenant configuration and internal RetailPulse tenant mapping separate and recoverable.
