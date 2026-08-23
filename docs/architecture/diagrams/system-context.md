# System Context

## Purpose

Shows the primary users, the store edge transaction boundary, cloud coordination, Azure dependencies, external payment, and the manager/owner PWA.

```mermaid
flowchart LR
    CASHIER[Cashier] --> EDGE[Store edge and POS]
    MANAGER[Store manager] --> PWA[Manager and owner PWA]
    OWNER[Owner] --> PWA
    EDGE --> CLOUD[RetailPulse cloud on AKS]
    PWA --> CLOUD
    EDGE --> PSP[External payment processor and terminal]
    CLOUD --> AZURE[Managed Azure dependencies]
    CLOUD --> AI[Azure OpenAI]
    PWA -. read and command APIs .-> CLOUD
    EDGE -. sync when connected .-> CLOUD
```

Ownership: Architecture. Status: Proposed.
