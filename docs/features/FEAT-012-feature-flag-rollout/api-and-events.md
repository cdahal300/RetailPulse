# FEAT-012: API and Event Contracts

## APIs and commands

- Internal flag interface: `EvaluateFlag(key, context)`, `GetSnapshot(version)`, and `PublishSnapshot(scope)` are provider-neutral application contracts.
- Admin APIs/commands: create, update, approve, evaluate, audit, expire, and rollback flags; production changes require separate approval.
- No new public business API is required; the abstraction must remain compatible with Azure App Configuration Feature Management and OpenFeature-style providers.
- Authentication and authorization: read/evaluate, change, approve, and emergency rollback permissions are distinct and tenant/environment scoped.
- Idempotency behavior: flag changes use change ID/version and conditional writes; duplicate activation/rollback returns the existing outcome.
- Error model: stable unknown-key, invalid-target, forbidden, stale-version, provider-unavailable, signature-invalid, and expired-snapshot results.

## Events

- Publishes `FeatureFlagChanged.v1`, `FeatureFlagApproved.v1`, and `FeatureFlagSnapshotPublished.v1` as audit/operational events where event integration is enabled.
- Producer: flag management service; consumers: edge snapshot distribution, audit, deployment operations, and observability.
- Required metadata: event ID, aggregate ID, store ID where scoped, occurred time, correlation ID, and schema version; include flag key/version, not secret values.
- Delivery and ordering: durable delivery ordered by flag key/version; edge accepts only newer authenticated snapshots.
- Duplicate handling: deduplicate change/event ID and apply monotonic flag/snapshot version.

## Compatibility

- Additive-change policy: optional targeting attributes and provider adapters must default safely; preserve existing keys and meanings.
- Breaking-change policy: version flag schemas/snapshot formats and support old edge snapshots through the defined cache window.
- Contract-test location: `tests/Contract/RetailPulse.ContractTests`; provider/edge snapshot tests in `tests/Integration/RetailPulse.IntegrationTests`.
- Ownership: release/platform team owns flag service and abstraction; feature teams own flag semantics and cleanup issue.
