# FEAT-001 API and Event Contracts

## Local application commands

These are internal edge commands and are not public cloud APIs.

```text
StartCheckout(cartId, terminalId)
CapturePayment(cartId, amount, paymentMethod)
CommitSale(cartId, paymentReference)
RetrySync(localTransactionId)
```

Expected payment adapter result:

```json
{
  "status": "Approved",
  "providerTransactionReference": "external-reference",
  "authorizationCode": "non-sensitive-provider-value"
}
```

The adapter result must never contain or persist raw card data.

## Cloud sync command

```json
{
  "commandType": "CompleteSale",
  "commandId": "command-uuid",
  "idempotencyKey": "store-001-terminal-02-local-txn-10452",
  "storeId": "store-001",
  "terminalId": "terminal-02",
  "localTransactionId": "local-txn-10452",
  "occurredAt": "2026-08-22T10:15:00Z",
  "schemaVersion": 1,
  "sale": {
    "currency": "USD",
    "subtotalMinor": 2500,
    "taxMinor": 200,
    "totalMinor": 2700,
    "lines": [
      {
        "productId": "product-001",
        "quantity": 1,
        "unitPriceMinor": 2500,
        "taxCategory": "standard"
      }
    ]
  },
  "payment": {
    "providerTransactionReference": "external-reference",
    "status": "Approved"
  }
}
```

## SaleCompleted.v1 event

```json
{
  "eventType": "SaleCompleted",
  "eventId": "event-uuid",
  "aggregateId": "sale-uuid",
  "storeId": "store-001",
  "occurredAt": "2026-08-22T10:15:00Z",
  "schemaVersion": 1,
  "correlationId": "correlation-uuid",
  "source": "store-edge",
  "saleId": "sale-uuid",
  "localTransactionId": "local-txn-10452",
  "currency": "USD",
  "totalMinor": 2700,
  "paymentReference": "external-reference",
  "inventoryMovements": [
    {
      "productId": "product-001",
      "quantityDelta": -1
    }
  ]
}
```

## Contract rules

- `schemaVersion` is required on every event.
- `eventId`, `aggregateId`, `storeId`, and `occurredAt` are required.
- Monetary values use integer minor units and an explicit currency.
- Consumers must tolerate additive fields.
- Breaking changes require a new event version and a compatibility test.
- Payment references are opaque external identifiers; they are not card data.
- The cloud command handler must enforce idempotency by `idempotencyKey`.
