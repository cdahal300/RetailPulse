# FEAT-011: External Payment-Provider Adapter

## Outcome

As a cashier, I want RetailPulse to work with a selected certified terminal and PSP without RetailPulse handling card data.

## Status

Adapter boundary and sandbox mapping implemented; provider certification, hardware integration, refunds, and production reconciliation remain pending.

## Verified Slice

- External gateway accepts only amount, currency, terminal/transaction context, correlation ID, and idempotency key.
- Adapter maps approved, declined, pending, cancelled, and timeout responses.
- Duplicate idempotency keys do not invoke the gateway twice.
- Approved responses require an opaque provider reference.
- No PAN, CVV, PIN, magnetic-stripe, or raw card fields are present in the adapter contract.
- Edge runtime defaults to the local sandbox gateway and can select Stripe test mode through `Payment__Provider=Stripe`.
- Stripe test credentials resolve from Key Vault secret `Payment--Stripe--ApiKey`.
- Authorized Edge payment authorization is exposed without accepting card data and emits a versioned payment event.

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
