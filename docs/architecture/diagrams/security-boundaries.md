# Security Boundaries

## Purpose

Shows trust zones and data classification. Payment-card data is captured and handled outside RetailPulse; only approved references cross the adapter boundary.

```mermaid
flowchart LR
    CASHIER[Cashier and checkout device]
    EDGE[Store edge]
    TLS[TLS transport boundary]
    AKS[AKS application workloads]
    SERVICES[Managed Azure services]
    PAYMENT[External terminal and payment processor]
    SECRETS[Key Vault and workload identity]
    PII[Limited business and personal data]
    CARD[Payment-card data external only]
    CASHIER --> EDGE
    EDGE --> TLS --> AKS
    AKS --> SERVICES
    SECRETS -. secret access .-> AKS
    EDGE --> PAYMENT
    PAYMENT --> CARD
    EDGE --> PII
    AKS --> PII
    CARD -. never persisted by RetailPulse .-> PAYMENT
    EDGE -. payment reference only .-> AKS
```

Controls include device and user authentication, authorization at each API, encryption in transit and at rest, secret isolation, structured audit records, and data minimization. Exact retention and regulatory obligations are decision required. Ownership: Security. Status: Proposed.
