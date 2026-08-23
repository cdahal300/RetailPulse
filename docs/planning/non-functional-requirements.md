# Non-Functional Requirements

All values below are proposed targets and require pilot validation.

| ID | Area | Proposed target | Measurement |
|---|---|---|---|
| NFR-001 | Checkout | Local cart and total response p95 <= 300 ms; local commit p95 <= 1 s | Edge telemetry and device test |
| NFR-002 | Durability | 100% of successful local commits have sale, payment reference, receipt intent, inventory movement, and outbox record | SQLite integration tests |
| NFR-003 | Sync | 99% of transiently failed messages accepted within 15 minutes after connectivity recovery; zero duplicate business effects | Sync metrics and reconciliation |
| NFR-004 | Availability | Edge checkout availability >= 99.9% during staffed operating hours, excluding hardware failure | Edge health and store calendar |
| NFR-005 | Cloud availability | Cloud API monthly availability >= 99.9% for management workflows | Monitor availability test |
| NFR-006 | RPO | Edge transaction RPO is 0 after a successful local commit; cloud reporting RPO <= 15 minutes | Recovery exercise |
| NFR-007 | RTO | Edge restart recovery <= 5 minutes; cloud service recovery <= 60 minutes | Recovery exercise |
| NFR-008 | Freshness | Operational read models show freshness <= 5 minutes online; offline status is explicit | Event timestamps |
| NFR-009 | AI latency | Insight generation p95 <= 30 seconds and never blocks checkout | Worker telemetry |
| NFR-010 | Telemetry overhead | Edge checkout latency overhead <= 5%; no raw card data in logs | Benchmark and log review |
| NFR-011 | Budget | Monthly pilot cloud budget target <= $1,000; alert at 80% and hard review at 100%; AI usage alert at $50/month | Azure cost data |
| NFR-012 | Tenant isolation | Zero unauthorized cross-tenant or cross-store reads, writes, events, cache hits, exports, or analytics results | Security, integration, contract, and recovery tests |
| NFR-013 | Scope propagation | 100% of tenant/store-scoped commands and events include validated scope, correlation ID, and schema version | Contract tests and telemetry review |

Targets depend on transaction volume, hardware, region, provider, and retention decisions.
