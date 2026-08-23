# Glossary

| Term | Meaning |
|---|---|
| Edge | Store-local runtime and persistence boundary used by checkout |
| Cloud | AKS-hosted APIs, workers, read models, and coordination services |
| Outbox | Durable local records of events waiting for synchronization |
| Sync | Idempotent movement of accepted edge transactions and events to cloud |
| Payment reference | Non-card identifier returned by an external payment provider |
| PWA | Responsive manager and owner Progressive Web App |
| Read model | Query-optimized representation for operational or reporting views |
| Domain event | Versioned fact containing event, aggregate, store, time, and schema identity |
| Conflict record | Durable record of a synchronization mismatch needing policy or review |
| Feature flag | Audited runtime activation control separate from deployment |
| Insight | Governed, advisory analytics or AI output |
| PSP | External payment service provider; selection is TBD |
| RPO | Maximum acceptable data loss measured in time |
| RTO | Maximum acceptable recovery time |
| ADLS | Azure Data Lake Storage used for raw and curated analytics zones |
