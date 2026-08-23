# Release Plan

1. Create a short-lived feature branch from protected `main` and complete Definition of Ready for the vertical slice and its contracts.
2. Build and test edge behavior with a fake payment adapter.
3. Deploy immutable images to development and staging through CI/CD.
4. Verify migrations, health checks, telemetry, secrets, and rollback.
5. Validate terminal/provider integration in an approved test environment.
6. Enable feature flags for internal users, then one pilot store.
7. Monitor checkout, sync, payment reconciliation, availability, and cost.
8. Expand only after the pilot gate is approved.
9. Disable flags or roll back the image if health or business thresholds fail.

The same immutable commit or image is promoted from development to staging to production. Branches do not represent environments; feature flags control exposure separately. See [branching strategy](branching-strategy.md).

Promotion approvals, gate thresholds, environment names, and release calendar are decision required. See [ADR 004](../architecture/decisions/004-feature-flags-for-controlled-release.md).
