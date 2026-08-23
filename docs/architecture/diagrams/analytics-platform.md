# Analytics Platform

## Purpose

Shows the governed path from versioned business events to operational aggregates, read models, and asynchronous AI insights.

```mermaid
flowchart LR
    EVENTS[Versioned domain events] --> INGEST[Ingestion and validation]
    INGEST --> RAW[ADLS raw zone]
    RAW --> CURATED[ADLS curated zone]
    CURATED --> AGG[Daily and weekly aggregates]
    AGG --> QUALITY[Data quality checks]
    QUALITY --> READ[Analytics read models]
    READ --> PWA[Manager and owner PWA]
    AGG --> AI[Azure OpenAI insight service]
    AI --> GUARD[Schema validation and audit]
    GUARD --> READ
    QUALITY --> QUAR[Quarantine and review]
```

AI is advisory and asynchronous. It cannot approve payments, set prices, mutate inventory, or block checkout. Ownership: Analytics. Status: Proposed.
