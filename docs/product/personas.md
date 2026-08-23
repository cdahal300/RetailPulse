# Personas

| Persona | Needs | MVP boundaries |
|---|---|---|
| Cashier | Fast, dependable checkout and clear payment or sync status | Uses POS and edge only; no cloud dependency for local checkout |
| Store manager | Daily operations, stock visibility, configuration, and recovery status | Uses authenticated PWA; commands are authorized and may be pending |
| Owner | Cross-store performance and concise explanations | Uses PWA read models and advisory insights |
| Support operator | Diagnose failed sync, payment reconciliation, and releases | Uses operational tools and runbooks; access is audited |
| Platform operator | Deploy, observe, recover, and control rollout | Owns AKS, environments, telemetry, and release controls |

Persona permissions and tenant/store assignment are defined by FEAT-005 and remain decision required until identity policy is adopted.
