# Tenant Isolation Checklist

Use this checklist for every feature and pull request that handles tenant or store data.

## Request and authorization

- [ ] Scope comes from validated identity/device context.
- [ ] Client filters are intersected with permitted scope.
- [ ] Role, tenant, and store authorization is enforced server-side.
- [ ] Cross-tenant and cross-store denial tests exist.

## Storage and messaging

- [ ] Every tenant-owned record has `tenantId`; store-owned records have `storeId`.
- [ ] Repository methods require scope rather than accepting it as optional metadata.
- [ ] Unique keys include the required tenant/store boundary.
- [ ] Events and commands contain scope and consumers revalidate it.
- [ ] Outbox, retry, dead-letter, replay, and conflict flows preserve scope.

## Cache, analytics, and AI

- [ ] Cache keys include tenant and store scope.
- [ ] Analytics partitions and queries enforce tenant/store scope.
- [ ] Exports are scoped, authorized, and audited.
- [ ] AI inputs contain only authorized, minimum aggregated data.
- [ ] Feature-flag targeting cannot leak values across scopes.

## Operations and recovery

- [ ] Logs and telemetry carry safe scope dimensions without sensitive data.
- [ ] Backup, restore, reprocessing, and migration tests preserve boundaries.
- [ ] Alerts and dashboards do not expose another tenant's data.
- [ ] Security review evidence is attached before production rollout.