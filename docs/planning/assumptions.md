# Assumptions

| ID | Assumption | Validation owner | State |
|---|---|---|---|
| ASM-001 | Checkout must function through temporary internet outages | Product and store operations | To validate |
| ASM-002 | Stores can host a local edge runtime and supported hardware | Store operations | To validate |
| ASM-003 | A certified external payment terminal and provider are available in the United States | Payments | To validate; shortlist in decision log |
| ASM-004 | Cloud APIs and workers can run on AKS | Platform | Proposed |
| ASM-005 | PostgreSQL, Service Bus, Blob, Key Vault, App Configuration, Monitor, and Azure OpenAI are acceptable dependencies | Platform and security | Decision required |
| ASM-006 | Managers and owners can use a responsive PWA | Product | To validate |
| ASM-007 | Eventual consistency is acceptable for cloud reporting | Product and finance | Accepted for MVP; freshness is shown |
| ASM-008 | AI output can remain advisory and asynchronous | Product and security | Proposed |
| ASM-009 | Pilot volume, operating hours, and retention exceptions will be supplied before sizing | Product | Open |
