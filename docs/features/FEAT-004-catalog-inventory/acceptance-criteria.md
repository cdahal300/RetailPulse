# FEAT-004: Acceptance Criteria

## Functional behavior

- Given an active product and barcode, when a cashier searches locally, then the current effective name, price, tax category, and stock view are returned offline.
- Given an authorized receipt or adjustment, when inventory changes, then an immutable movement and updated balance are produced with effective time and reason.
- Given a low-stock threshold, when available stock crosses it, then the operational read model identifies the product and store.

## Failure and resilience behavior

- Given stale catalog data, when checkout occurs, then the edge shows its version/effective time and applies the documented stale-data policy.
- Given duplicate movement delivery or a conflicting version, when sync processes it, then one movement is retained and the conflict is reviewable without overwriting a completed sale.
- Given a rejected negative-stock operation, when submitted, then no balance mutation occurs and the reason is returned.

## Authorization and isolation

- Cashiers can read catalog and stock needed for checkout; managers can receive stock and adjust inventory; owners can configure thresholds across authorized stores.
- A command for another store or tenant is rejected server-side and cannot change local or cloud balances.

## Data and security

- Sensitive data handling: catalog and movement records contain no payment-card data; protect supplier and operational data according to classification.
- Audit requirements: record actor, store, reason, effective time, prior/version context, and every adjustment or conflict resolution.
- Retention and deletion: retain the movement ledger for reconciliation and apply documented catalog/archive retention; never delete movements to correct a balance.
- Validate barcodes, prices, tax categories, quantities, currencies, and timestamps at both API and domain boundaries.
