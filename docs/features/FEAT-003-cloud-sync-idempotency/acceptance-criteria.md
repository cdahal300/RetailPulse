# FEAT-003: Acceptance Criteria

## Functional behavior

- Given a committed edge outbox message, when connectivity is available, then sync submits it to the cloud and records synced status after durable acceptance.
- Given a store operator, when viewing sync health, then the oldest pending message, last successful sync, retry state, conflict count, and dead-letter count are visible.

## Failure and resilience behavior

- Given a timeout, transient 5xx, or disconnected store, when sync runs, then it retries with bounded exponential backoff and preserves the outbox message.
- Given a duplicate delivery, when the cloud receives it, then exactly one sale/effect is retained and the duplicate response is safe.
- Given an unrecoverable validation or conflict error, when retries are exhausted, then the message is dead-lettered or marked reviewable with its reason and correlation ID.

## Authorization and isolation

- Given a registered store and terminal identity, when syncing, then the cloud accepts only messages authorized for that store and tenant.
- Given a message from another store, unknown terminal, or invalid device credential, when submitted, then it is rejected without mutation or cross-store disclosure.

## Data and security

- Sensitive data handling: sync only approved business data and opaque payment references; never PAN, CVV, PIN, magnetic-stripe, or raw card data.
- Audit requirements: retain submission, acceptance, retry, conflict, dead-letter, and operator-review history with actor/device context.
- Retention and deletion: apply outbox and dead-letter retention windows; preserve records needed for financial reconciliation.
- Protect Service Bus credentials with managed identity or Key Vault and redact payloads from logs.
