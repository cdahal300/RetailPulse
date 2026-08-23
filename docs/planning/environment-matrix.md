# Environment Matrix

| Environment | Purpose | Data | External integrations | Flag policy | Access |
|---|---|---|---|---|---|
| Local | Developer feedback and unit/integration tests | Synthetic | Fake payment; local AI option | Safe defaults | Developers |
| Development | Shared integration | Synthetic or seeded | Sandboxed providers | Feature teams | Engineering |
| Staging | Release rehearsal and contract tests | Sanitized non-production | Test terminal/provider | Approval required | Engineering and QA |
| Pilot | Limited production validation | Pilot business data | Approved certified provider | Store-scoped activation | Approved operators |
| Production | Live operations | Production data | Approved integrations only | Audited approval | Least privilege |

MVP baseline: one US Azure region, one AKS cluster with environment namespaces, managed Azure dependencies, and daily backups. Production secrets must use Key Vault and workload identity. A secondary region is deferred until pilot scale and business RTO are confirmed.
