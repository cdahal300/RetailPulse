# RetailPulse Feature Roadmap

This roadmap defines the implementation order for the MVP. Each feature gets its own folder and must have acceptance criteria, contracts, QA coverage, and rollout guidance before implementation.

Feature folders currently begin as planning briefs. Before an implementation starts, complete the full Definition of Ready described in [README.md](README.md), using the reusable documents in `templates/`.

## Foundation and transaction safety

| ID | Feature | Primary boundary | Status |
|---|---|---|---|
| FEAT-001 | Reliable checkout and cloud recovery | Edge | In progress; domain slice implemented, SQLite remains |
| FEAT-002 | Durable SQLite edge persistence | Edge | Definition of Ready complete |
| FEAT-003 | Cloud synchronization and idempotency | Edge / Cloud | Definition of Ready complete |
| FEAT-004 | Catalog and inventory management | Edge / Cloud | Definition of Ready complete |
| FEAT-005 | Identity and role authorization | Cloud / Edge / PWA | Definition of Ready complete |

## Platform delivery

| ID | Feature | Primary boundary | Status |
|---|---|---|---|
| FEAT-006 | Azure platform and AKS infrastructure | Infrastructure | Definition of Ready complete |
| FEAT-007 | CI/CD and release automation | GitHub / AKS | Definition of Ready complete |
| FEAT-008 | OpenTelemetry observability | All runtimes | Definition of Ready complete |

## Experiences and intelligence

| ID | Feature | Primary boundary | Status |
|---|---|---|---|
| FEAT-009 | Manager and owner PWA | Web / PWA | Definition of Ready complete |
| FEAT-010 | Analytics and reporting | Cloud / Analytics | Definition of Ready complete |
| FEAT-011 | External payment-provider adapter | Edge / External | Definition of Ready complete; provider-dependent |
| FEAT-012 | Feature-flag controlled rollout | Platform | Definition of Ready complete; design accepted |
| FEAT-013 | AI insights | Analytics / Cloud | Definition of Ready complete; asynchronous only |

## Delivery rule

Implement in dependency order. FEAT-006 and FEAT-007 can begin in parallel with FEAT-002, but production rollout requires working health checks, telemetry, secrets, and rollback procedures. FEAT-010 must produce governed aggregates before FEAT-013 sends data to an AI model.
