# Data Ownership

## Purpose

Identifies core domain records and their authoritative boundary. Cloud acceptance and reporting do not remove the edge ownership needed for local checkout state and recovery.

```mermaid
flowchart LR
    subgraph EDGE[Store edge authority]
        STORE[Store]
        TERMINAL[Terminal]
        PRODUCT[Product]
        PRICE[Price]
        SALE[Sale]
        LINE[SaleLine]
        PAY[PaymentReference]
        MOVE[InventoryMovement]
        RECEIPT[ReceiptIntent]
        OUTBOX[OutboxMessage]
        ATTEMPT[SyncAttempt]
        CONFLICT[ConflictRecord]
    end
    subgraph CLOUD[Cloud authority and read models]
        FLAG[FeatureFlag]
        INSIGHT[Insight]
        CLOUDSALE[Accepted sale read model]
        CLOUDINV[Inventory read model]
    end
    STORE --> TERMINAL
    PRODUCT --> PRICE
    SALE --> LINE
    SALE --> PAY
    SALE --> MOVE
    SALE --> RECEIPT
    SALE --> OUTBOX
    OUTBOX --> ATTEMPT
    ATTEMPT --> CONFLICT
    OUTBOX -. sync .-> CLOUDSALE
    MOVE -. sync .-> CLOUDINV
    FLAG --> INSIGHT
    CLOUDSALE --> INSIGHT
```

Notes: Store and Terminal are edge configuration roots. Sale and its local effects are transaction-owned by the edge. Feature flags are cloud-managed with an authenticated edge snapshot. Insight is cloud-owned and advisory. Ownership: Domain and data. Status: Proposed.
