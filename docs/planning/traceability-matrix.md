# Traceability Matrix

| Outcome or control | Product or architecture source | Feature evidence | Planned validation |
|---|---|---|---|
| Reliable checkout and cloud recovery | [ADR 002](../architecture/decisions/002-offline-first-edge.md) | FEAT-001, FEAT-002 | Unit, integration, end-to-end |
| No card data | [ADR 003](../architecture/decisions/003-external-payment-provider.md) | FEAT-011 | Adapter and log review |
| Managed cloud host | [ADR 001](../architecture/decisions/001-use-aks.md) | FEAT-006 | Infrastructure and health checks |
| Controlled rollout | [ADR 004](../architecture/decisions/004-feature-flags-for-controlled-release.md) | FEAT-012 | Flag audit and rollback drill |
| Role boundaries | [PWA navigation](../architecture/diagrams/pwa-navigation.md) | FEAT-005, FEAT-009 | Authorization and PWA tests |
| Governed AI | [Analytics platform](../architecture/diagrams/analytics-platform.md) | FEAT-010, FEAT-013 | Data quality, schema, audit tests |
| Recovery and operations | [Runbooks](../architecture/runbooks/README.md) | FEAT-003, FEAT-008 | Game days and support rehearsal |
| Measurable quality | [NFRs](non-functional-requirements.md) | All relevant features | Telemetry and acceptance gates |
