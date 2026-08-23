# FEAT-011: External Payment-Provider Adapter

## Outcome

As a cashier, I want RetailPulse to work with a selected certified terminal and PSP without RetailPulse handling card data.

## Scope

- Adapter interface for authorization, cancellation, status, and supported refunds.
- Sandbox integration with the selected provider and certified terminal.
- Approved, declined, cancelled, pending, timeout, and reconciliation states.
- Opaque provider reference storage and provider error mapping.

## Acceptance criteria

- RetailPulse sends only amount and transaction context to the adapter.
- Card capture, tokenization, authorization, settlement, PCI scope, and acquiring remain external.
- No PAN, CVV, PIN, magnetic-stripe data, or raw card data enters code, storage, logs, events, or analytics.
- Provider timeouts and duplicate requests are handled safely.
- Production use is blocked until provider certification is complete.

## Dependencies and QA

Depends on FEAT-001 and FEAT-008. Provider sandbox and hardware tests are required; certification and transaction fees are external dependencies.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
