# Deployment Topology

## Purpose

Shows the proposed public and private boundaries for cloud workloads and managed dependencies. Exact SKUs, regions, node pools, and network ranges are decision required.

```mermaid
flowchart TB
    PUBLIC[Public trust boundary]
    PRIVATE[Private trust boundary]
    FD[Azure Front Door]
    AGW[Application Gateway]
    INGRESS[AKS ingress]
    PUBLIC --> FD --> AGW --> INGRESS
    subgraph AKS[AKS cluster]
        subgraph NSAPI[API namespace]
            API[Cloud API]
        end
        subgraph NSWORK[Worker namespace]
            WORKERS[Sync and workflow workers]
        end
        subgraph NSOBS[Operations namespace]
            HEALTH[Health and telemetry endpoints]
        end
        INGRESS --> API
        API --> WORKERS
    end
    ACR[Azure Container Registry] --> API
    ACR --> WORKERS
    ID[Managed workload identity] -.-> API
    ID -.-> WORKERS
    API --> PG[Managed PostgreSQL]
    API --> BUS[Azure Service Bus]
    WORKERS --> BLOB[Azure Blob Storage]
    API --> KV[Azure Key Vault]
    API --> CONFIG[Azure App Configuration]
    API --> MON[Azure Monitor and OpenTelemetry]
    WORKERS --> AOAI[Azure OpenAI]
    AKS[AKS cluster] --> PRIVATE
    PG --> PRIVATE
    BUS --> PRIVATE
    BLOB --> PRIVATE
    KV --> PRIVATE
    CONFIG --> PRIVATE
    MON --> PRIVATE
    AOAI --> PRIVATE
```

Ownership: Platform. Status: Proposed.
