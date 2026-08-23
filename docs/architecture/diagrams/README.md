# Architecture Diagrams

- [RetailPulse overall redesigned Draw.io diagram](retailpulse-overall-redesign.drawio): primary editable presentation view for product, architecture, and planning discussions.
- [RetailPulse overall original Draw.io diagram](retailpulse-overall.drawio): earlier detailed draft retained for reference.

These diagrams show runtime boundaries, ownership, data movement, security, release flow, and PWA navigation. They are implementation aids, not substitutes for contracts or ADRs.

## Index

| Diagram | Purpose | Owner |
|---|---|---|
| [System context](system-context.md) | People, products, and external systems | Architecture |
| [Deployment topology](deployment-topology.md) | AKS workloads and managed Azure dependencies | Platform |
| [Data ownership](data-ownership.md) | Domain entities and edge/cloud ownership | Domain and data |
| [Event topology](event-topology.md) | Outbox, synchronization, messaging, and replay | Edge and cloud |
| [Security boundaries](security-boundaries.md) | Trust zones and data classifications | Security |
| [Analytics platform](analytics-platform.md) | Governed event-to-insight flow | Analytics |
| [Release flow](release-flow.md) | Build, promotion, activation, and rollback | Delivery |
| [PWA navigation](pwa-navigation.md) | Manager and owner routes and role boundaries | Web |

Each diagram should name its owning team and be updated when the corresponding boundary changes. ADRs and contracts remain the authoritative records for decisions and interfaces.
