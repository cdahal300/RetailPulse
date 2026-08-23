# Keep Payment Processing External

* Status: Proposed
* Deciders: TBD
* Date: 2026-08-22

## Context and Problem Statement

RetailPulse needs to initiate payment for a sale and retain enough information for receipt and reconciliation. It must not become a payment processor or handle raw card data, and terminal/provider certification responsibilities must remain outside the application.

## Decision Drivers

- PAY-001: Keep card capture and authorization at the certified terminal and selected external processor.
- DATA-001: Never persist PAN, CVV, PIN, magnetic-stripe data, or raw card data.
- INT-001: Isolate provider-specific behavior behind an adapter interface.
- CERT-001: Preserve the payment provider and terminal certification boundary.

## Considered Options

### Option 1: External processor and certified terminal with a RetailPulse adapter

Pros: limits payment scope, permits provider substitution, and keeps checkout integration testable with a fake adapter. Cons: provider availability, terminal behavior, and certification requirements remain dependencies.

### Option 2: Build payment processing in RetailPulse

Rejected: it creates an inappropriate security, operational, financial, and certification boundary for the MVP.

### Option 3: Store raw card data for later processing

Rejected: it violates the data boundary and creates unnecessary exposure. RetailPulse stores only processor references and reconciliation state.

## Decision

Payment processing, terminal control for card capture, authorization, settlement, and acquiring remain external. RetailPulse calls an adapter that sends a sale amount and transaction request, receives approved, declined, cancelled, or pending outcomes, and stores a processor transaction reference plus reconciliation status. Refund behavior is limited to provider-supported workflows.

## Positive Consequences

- POS-001: RetailPulse avoids storing prohibited payment-card data.
- POS-002: Provider-specific SDK and API details stay out of domain logic.
- POS-003: Certification and card-handling responsibilities remain with the appropriate external parties.

## Negative Consequences

- NEG-001: Provider outages or terminal faults can prevent payment completion.
- NEG-002: Integration, certification, and reconciliation behavior depend on the selected provider.
- NEG-003: Pending and timeout outcomes require explicit operational recovery.

## Implementation Notes

- Use a payment adapter interface and fake adapter in development and automated tests.
- Persist only non-sensitive references, status, amount, and audit metadata approved by the provider contract.
- Validate provider responses and make retries safe according to provider idempotency support.
- Selected provider, terminal model, markets, and certification evidence are TBD.

## References

- [Architecture overview](../overview.md)
- [FEAT-011 Payment Provider Adapter](../../features/FEAT-011-payment-provider-adapter/README.md)
