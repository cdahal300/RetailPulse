# FEAT-004: Catalog and Inventory Management

## Outcome

As a cashier or manager, I want accurate local product and stock information so that sales and replenishment decisions remain useful offline.

## Scope

- Product catalog, prices, tax categories, active status, and barcode lookup.
- Inventory movement ledger and materialized stock balance.
- Catalog and inventory synchronization to the edge.
- Manager stock receipts and adjustments with authorization.
- Low-stock thresholds and conflict records.

## Acceptance criteria

- Cashier lookup uses local catalog data during checkout.
- Every stock change is an auditable movement, not a silent overwrite.
- Price and catalog changes have effective timestamps and version handling.
- Manager-only adjustments are authorized and logged.
- Conflicts never overwrite a completed sale silently.

## Dependencies and QA

Depends on FEAT-003 and FEAT-005. Test stale catalogs, price changes during offline periods, duplicate movements, negative stock policy, and reconciliation.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
