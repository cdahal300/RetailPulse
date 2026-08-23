# FEAT-011: Acceptance Criteria

## Functional behavior

- Given an approved amount and transaction context, when the adapter requests authorization, then the certified terminal/PSP returns mapped approved, declined, cancelled, pending, or failed status and an opaque provider reference.
- Given a supported refund, when the adapter submits it, then the refund is tied to the provider reference and reconciliation state without exposing card data.
- Given a pending provider result, when status is queried, then the POS shows unresolved state and does not create a second authorization.

## Failure and resilience behavior

- Given timeout, network loss, terminal disconnect, or provider 5xx, then the transaction remains pending/unknown for reconciliation and is never marked approved without provider evidence.
- Given duplicate authorization or retry, then provider idempotency and local transaction identity prevent double charge; cancellation/status recovery is attempted safely.
- Given provider outage or unsupported response, checkout follows documented decline/pending policy and exposes an operator-review path.

## Authorization and isolation

- Only authorized checkout services/devices can invoke payment commands; manager refund permissions are separate and server/edge enforced.
- The adapter cannot be used to bypass amounts, currency, store, terminal, authorization, audit, or feature-flag controls; provider credentials are scoped per environment/store.

## Data and security

- Sensitive data handling: send only amount, currency, transaction context, and required terminal metadata; never accept, persist, log, emit, or analyze PAN, CVV, PIN, magnetic-stripe, or raw card data.
- Audit requirements: record actor/device, amount/currency, local transaction ID, provider reference, status transitions, errors, and reconciliation actions without card data.
- Retention and deletion: retain opaque references and financial reconciliation records per policy; purge transient request data and sandbox artifacts.
- Use provider-certified SDK/terminal boundary, TLS, secret management, strict logging redaction, and PCI scope review before production.
