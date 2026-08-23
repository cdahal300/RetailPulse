---
name: RetailPulse QA review
description: Review a RetailPulse change for functional, offline, integration, security, and observability risks.
agent: RetailPulse QA Reviewer
---

Review the current change for:

- Duplicate delivery and idempotency failures
- Offline checkout and sync recovery behavior
- Payment boundary and sensitive data leakage
- Event and API compatibility
- Authorization and tenant/store isolation
- Missing unit, integration, contract, or end-to-end coverage
- Missing logs, metrics, traces, and actionable alerts
- PWA behavior across responsive layouts, authentication, offline cached reads, push notifications, service-worker lifecycle, accessibility, and mobile browser differences

Report findings first by severity with file references, then test gaps and assumptions. Do not modify files unless explicitly requested.
