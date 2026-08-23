# FEAT-011: API and Event Contracts

## APIs and commands

- Adapter interface: `Authorize`, `Cancel`, `GetStatus`, and supported `Refund` accept amount, currency, transaction ID, terminal context, and idempotency key; they return status and opaque provider reference.
- No public card-processing API is exposed by RetailPulse; provider/terminal APIs are external contracts and remain behind the adapter.
- Authentication and authorization: payment service/device identity, store/terminal binding, refund policy, and environment-specific provider credentials are required.
- Idempotency behavior: local transaction ID plus provider idempotency key is reused across retries; uncertain outcomes require status lookup before a new authorization.
- Error model: stable approved, declined, cancelled, pending, timeout, provider-unavailable, invalid-request, and reconciliation-required states; no raw provider payloads in clients/logs.

## Events

- Publishes `PaymentAuthorizationCompleted.v1`, `PaymentStatusChanged.v1`, and `PaymentRefundCompleted.v1` with opaque references when applicable.
- Producer: payment adapter/domain boundary; consumers: checkout, reconciliation, sync, reporting, and audit.
- Required metadata: event ID, aggregate ID, store ID, occurred time, correlation ID, and schema version; payment fields exclude card data.
- Delivery and ordering: durable delivery ordered per local transaction/payment aggregate; pending transitions are explicit.
- Duplicate handling: deduplicate event ID and provider reference; conflicting provider states enter reconciliation review.

## Compatibility

- Additive-change policy: add provider-neutral optional fields and preserve status meanings; never expose provider-specific card fields.
- Breaking-change policy: version adapter/event contracts and support old states until pending transactions reconcile.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; adapter/provider tests in `tests/Integration/RetailPulse.IntegrationTests`.
- Ownership: Payment integration owns adapter and provider mapping; checkout owns transaction lifecycle; provider owns card capture/certification.
