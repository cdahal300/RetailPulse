# FEAT-001 Acceptance Criteria

## Product and cart

- Given a locally cached active product, when the cashier scans or searches for it, then the product name, price, and tax category are shown.
- Given a product is unavailable locally, when the cashier searches for it, then the POS explains that the product is unavailable instead of making a cloud request during checkout.
- Given valid cart lines, when the cashier reviews the cart, then subtotal, tax, discount, and total are calculated using the configured store rules.
- Given an invalid quantity or inactive product, when the cashier tries to add it, then the cart rejects the action with a clear reason.

## Payment boundary

- Given a cart total, when checkout starts, then the POS sends only the amount and transaction context to the payment adapter.
- Given the adapter returns approved, then the POS stores only the provider transaction reference and non-sensitive status.
- Given the adapter returns declined, cancelled, or timed out, then the sale is not marked paid and the cashier can retry or cancel.
- Given payment is pending, then the POS shows pending status and does not create a completed sale until the adapter resolves it.
- The local database and logs contain no PAN, CVV, PIN, magnetic-stripe data, or raw card data.

## Offline durability

- Given the cloud is unavailable, when the external provider returns a valid approved payment result, then the POS commits the sale locally without a cloud round trip.
- Given the payment provider is unavailable or does not return an approval, then the POS does not mark the sale paid or fabricate a payment result.
- Given a local commit succeeds, then the POS creates the sale, payment reference, inventory movement, receipt intent, and outbox message atomically.
- Given the process stops after commit, when it restarts, then the sale remains present and the outbox message remains pending.
- Given the local database commit fails, then the POS does not print a completed receipt or report the sale as complete.

## Synchronization

- Given a pending outbox message and restored connectivity, then the sync agent sends the command with a stable idempotency key.
- Given the cloud receives the same idempotency key more than once, then it accepts the command once and returns the existing result for retries.
- Given synchronization fails temporarily, then the message remains pending and retry metadata is recorded.
- Given synchronization succeeds, then the outbox message is marked synced and the POS shows the cloud-confirmed status.
- Given a conflict or unrecoverable validation failure, then the message is moved to an explicit review state and is not silently discarded.

## Authorization and observability

- Given a cashier without manager permissions, when they attempt a manager-only adjustment, then access is denied.
- Each checkout has a correlation ID and local transaction ID in structured logs.
- The system records payment outcome, local commit latency, outbox depth, sync age, and sync failures without recording card data.
