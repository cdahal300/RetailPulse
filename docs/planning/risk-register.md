# Risk Register

| ID | Risk | Impact | Mitigation | Owner | Status |
|---|---|---|---|---|---|
| RSK-001 | Local database loss or corruption | Lost or delayed sales | Atomic writes, restart tests, backup and recovery runbook | Edge | Open |
| RSK-002 | Sync duplicates or conflicts | Incorrect cloud records | Idempotency keys, conflict records, replay tests | Edge and cloud | Open |
| RSK-003 | Terminal or PSP outage | Payment cannot complete | Provider timeout states, reconciliation, operator procedure | Payments | Open |
| RSK-004 | AKS operational complexity | Slow recovery or outages | Managed dependencies, health checks, platform ownership | Platform | Open |
| RSK-005 | Unauthorized PWA commands | Business or data impact | Central authorization, role tests, audit | Security | Open |
| RSK-006 | Sensitive data in logs or AI prompts | Privacy and security exposure | Redaction, data minimization, review gates | Security | Open |
| RSK-007 | Analytics quality defects | Misleading reports or insights | Versioned events, quality checks, quarantine | Analytics | Open |
| RSK-008 | Cost exceeds pilot budget | Delivery or operating constraint | Proposed budgets, alerts, usage review | Product and platform | Open |
| RSK-009 | Unclear market or fiscal obligations | Rework or launch delay | Decide market, tax, fiscal, retention scope | Product | Open |
| RSK-010 | Feature flag drift | Inconsistent behavior | Owner, expiry, audit, deterministic edge fallback | Release | Open |
