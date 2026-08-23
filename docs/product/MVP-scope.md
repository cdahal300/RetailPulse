# MVP Scope

## Target users

- Cashiers who need reliable checkout at a store register.
- Store managers who monitor sales, inventory, sync health, and configuration.
- Owners who review multi-store performance and governed insights.
- Operations and support staff who investigate synchronization, payments, and releases.

## Product model

RetailPulse is a multi-tenant SaaS product for multiple independent retail businesses. Each tenant represents one retail business and can own multiple stores, terminals, users, products, sales, inventory records, reports, feature-flag scopes, and AI insights.

```text
Tenant
├── Stores
│   └── Terminals
├── Users and roles
├── Catalog and pricing
├── Sales and inventory
├── Reports and insights
└── Configuration and feature flags
```

The normal user experience is tenant-scoped. Owners may see only stores belonging to their tenant; managers see assigned stores; cashiers and devices see one store. Cross-tenant access is not a customer feature and requires a separately authorized internal-operations boundary with explicit audit controls.

## Pilot assumptions

- Pilot stores have a local edge runtime and supported scanner, printer, cash drawer, and certified payment terminal.
- A selected external payment provider is decision required; the MVP target market is the United States.
- Catalog, pricing, tax, refund, retention, and fiscal rules are provided by the pilot business.
- Initial store count, transaction volume, and support coverage are TBD.
- The pilot includes multiple tenants, with at least one test tenant boundary exercised in every environment.

## In scope

Reliable checkout and cloud recovery, local SQLite persistence and outbox, cloud synchronization and idempotency, catalog and inventory, identity and roles, AKS platform, CI/CD, telemetry, manager/owner PWA, analytics, external payment adapter, controlled rollout, and asynchronous AI summaries or explanations.

Multi-tenant onboarding, tenant/store authorization, scoped data storage, tenant-safe events and caches, tenant-partitioned analytics, and tenant-scoped operational support are also in scope.

## Out of scope

Payment processing, card data storage, unrestricted PWA checkout, native mobile apps, arbitrary offline refunds, autonomous AI decisions, and uncommitted tax or fiscal integrations.

## Supported device and market assumptions

POS hardware: Windows 11 x64 register with scanner, printer, cash drawer, and selected US-certified terminal. Manager and owner access: modern desktop, tablet, Android, and iOS browsers. MVP currency: USD. Store timezone is required. State/local tax model and fiscal requirements remain pilot-gate decisions.

## Multi-tenant success measures

- Zero unauthorized cross-tenant or cross-store reads, writes, events, cache results, exports, or analytics results.
- Tenant and store scope is present and validated across all tenant-owned commands and events.
- A tenant can be onboarded, configured, operated, exported, and removed without affecting another tenant.

## Success measures

Proposed measures are checkout completion during outages, local commit success, sync acceptance and recovery time, duplicate-sale rate, inventory discrepancy rate, PWA task completion, uptime, support burden, and insight usefulness. Baselines and thresholds are decision required.

## Open decisions

Provider and terminal, pilot stores, tax/fiscal scope, exact legal retention period, target volumes, support model, and final budget are decision required. The US market and USD baseline are accepted.
