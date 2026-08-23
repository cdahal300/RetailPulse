# Dependency Map

| Dependency | Enables | Depends on | Delivery order |
|---|---|---|---|
| FEAT-001 Reliable checkout and cloud recovery | Local sale outcome | Edge domain and payment adapter seam | 1 |
| FEAT-002 SQLite persistence | Durable local transaction and outbox | FEAT-001 | 2 |
| FEAT-003 Sync and idempotency | Cloud acceptance | FEAT-002 and contracts | 3 |
| FEAT-004 Catalog and inventory | Product and stock behavior | FEAT-002, FEAT-003 | 4 |
| FEAT-005 Identity | Role controls | Cloud API and edge auth | 5 |
| FEAT-006 AKS infrastructure | Cloud hosting | Contracts and environment config | Parallel |
| FEAT-007 CI/CD | Repeatable promotion | FEAT-006 and tests | Parallel |
| FEAT-008 Observability | Operability evidence | All runtimes | Cross-cutting; may be deferred after dev deployment but is required before production traffic |
| FEAT-009 PWA | Manager and owner workflows | FEAT-005 and read models; benefits from FEAT-008 health signals | 6 |
| FEAT-010 Analytics | Governed aggregates | FEAT-003 and event contracts | Recommended next if FEAT-008 is deferred |
| FEAT-011 Payment adapter | Certified terminal integration | Provider selection and FEAT-001 | 2-4 |
| FEAT-012 Feature rollout | Controlled activation | FEAT-006, FEAT-007, FEAT-008 | Cross-cutting |
| FEAT-013 AI insights | Advisory output | FEAT-010 and guardrails | 8 |
