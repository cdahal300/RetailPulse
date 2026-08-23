# FEAT-001 Rollout Plan

## Feature flag

- Flag key: `checkout.offline.v1`
- Default state: Off for all environments until automated tests pass
- Owner: Edge and Sales module owner
- Risk: Critical transaction path
- Expiry: Remove after the pilot rollout is complete

## Deployment sequence

1. Merge the feature behind the disabled flag.
2. Build and scan the immutable edge and cloud images.
3. Deploy to the development environment.
4. Run unit, SQLite integration, contract, and sync-retry tests.
5. Enable for staging and execute the offline checkout test matrix.
6. Enable for one internal pilot store with a fake or sandbox payment adapter.
7. Confirm local commit durability, receipt behavior, outbox depth, sync latency, and duplicate delivery metrics.
8. Enable for additional pilot stores gradually.
9. Complete provider certification before using the real terminal in production.
10. Remove the flag and dead code only after the rollout is stable.

## Rollback

1. Disable `checkout.offline.v1` for the affected store or environment.
2. Keep already committed local sales in the outbox; do not delete or replay them manually.
3. Investigate payment references and sync state through the reconciliation workflow.
4. Revert the deployment only if the code version is unsafe, because disabling the flag is the first rollback action.

## Metrics and alerts

- Local checkout success and failure rate
- Payment adapter approval, decline, cancellation, and timeout counts
- Local commit latency
- Pending outbox count and oldest outbox age
- Sync retry count and sync conflict count
- Duplicate command rate
- Receipt failure count
- Local storage capacity and database error count

## Release gates

- No sensitive payment data found in database or structured logs.
- Duplicate delivery tests pass.
- Process restart recovery test passes.
- Local cart and recovery behavior works without the cloud API; payment approval is never fabricated when the provider is unavailable.
- Payment-provider sandbox tests pass.
- Receipt and reconciliation procedures are documented.
- Edge and cloud schema versions remain compatible during rollout.
