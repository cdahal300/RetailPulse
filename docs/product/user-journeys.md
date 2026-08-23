# User Journeys

## Cashier completes an offline sale

1. Cashier scans locally available products.
2. POS calculates totals using local catalog and pricing.
3. Payment is requested through the certified terminal and adapter.
4. Edge commits sale, payment reference, inventory movement, receipt intent, and outbox atomically.
5. Receipt is printed or displayed and the sale shows sync-pending when disconnected.
6. Sync later accepts the idempotent command and updates status.

## Manager investigates low stock

1. Manager signs in to the PWA.
2. Dashboard shows current read model and freshness.
3. Manager opens low-stock detail and supporting values.
4. Manager submits an authorized restock or catalog command when supported.
5. The UI shows accepted, rejected, or pending status.

## Owner reviews an insight

1. Owner opens reporting and selects a store or period.
2. Aggregates and data-quality status are shown.
3. Owner opens an advisory insight with supporting facts and model metadata.
4. Owner treats the insight as guidance; no checkout or inventory mutation occurs automatically.

## Support recovers synchronization

1. Operator identifies a failed or dead-lettered message.
2. Operator inspects attempts, validation errors, and conflict record.
3. Operator resolves the business issue or authorizes replay.
4. Replay is idempotent and leaves an audit trail.
