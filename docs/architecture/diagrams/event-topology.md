# Event Topology

## Purpose

Shows how an edge transaction becomes a durable event, is synchronized idempotently, and is processed with retry, dead-letter, and replay paths.

```mermaid
flowchart LR
    TX[Edge transaction] --> OUTBOX[Durable outbox]
    OUTBOX --> SYNC[Sync agent]
    SYNC --> API[Cloud sync endpoint]
    API --> BUS[Azure Service Bus]
    BUS --> SALES[Sales consumer]
    BUS --> INVENTORY[Inventory consumer]
    BUS --> ANALYTICS[Analytics consumer]
    SALES --> READ[Read models]
    INVENTORY --> READ
    ANALYTICS --> LAKE[Analytics storage]
    BUS --> RETRY[Retry policy]
    RETRY --> BUS
    RETRY --> DLQ[Dead-letter queue]
    DLQ --> REVIEW[Operator review]
    REVIEW --> REPLAY[Replay command]
    REPLAY --> BUS
    API -. duplicate key returns prior result .-> SYNC
```

Events require stable IDs, aggregate IDs, store IDs, occurred time, schema version, and idempotency behavior. Ownership: Edge and cloud. Status: Proposed.
