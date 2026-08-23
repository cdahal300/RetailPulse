# PWA Navigation

## Purpose

Defines the manager and owner PWA navigation boundary. Checkout, terminal control, and payment authorization are explicitly excluded.

```mermaid
flowchart TB
    LOGIN[Sign in] --> ROLE{Authorized role}
    ROLE --> MANAGER[Manager workspace]
    ROLE --> OWNER[Owner workspace]
    MANAGER --> DASH[Sales and operations dashboard]
    MANAGER --> INV[Inventory and low stock]
    MANAGER --> CONFIG[Product and store configuration]
    MANAGER --> SYNC[Sync health]
    OWNER --> REPORTS[Sales and reporting]
    OWNER --> INSIGHTS[AI insights]
    OWNER --> STORES[Store overview]
    PWA[PWA boundary] -. excluded .-> CHECKOUT[Checkout control]
    PWA -. excluded .-> TERMINAL[Payment terminal control]
    PWA -. excluded .-> OFFLINE[Unrestricted offline mutation]
```

Offline PWA behavior is limited to a small read cache and explicitly supported queued commands with pending status. Ownership: Web and identity. Status: Proposed.
