# PWA Navigation

## Purpose

Defines the manager and owner PWA navigation boundary. Checkout, terminal control, and payment authorization are explicitly excluded.

```mermaid
flowchart TB
    LOGIN[Sign in] --> ROLE{Authorized role}
    ROLE --> MANAGER[Manager workspace]
    ROLE --> OWNER[Owner workspace]
    MANAGER --> DASH[Priority-first overview]
    MANAGER --> INV[Inventory worklist]
    MANAGER --> ALERTS[Alerts and notifications]
    MANAGER --> SYNC[Sync health and recovery]
    OWNER --> REPORTS[Sales and reporting]
    OWNER --> INSIGHTS[AI insights]
    OWNER --> STORES[Cross-store overview]
    OWNER --> CONFIG[Owner store settings]
    PWA[PWA boundary] -. excluded .-> CHECKOUT[Checkout control]
    PWA -. excluded .-> TERMINAL[Payment terminal control]
    PWA -. excluded .-> OFFLINE[Unrestricted offline mutation]
```

Offline PWA behavior is limited to a small read cache and explicitly supported queued commands with pending status. The modern view plan is documented in `docs/features/FEAT-009-manager-owner-pwa/modern-view-plan.md`. Ownership: Web and identity. Status: Planned.
