# Feature Specifications

Each feature starts here as a small, reviewable product specification before implementation begins.

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the complete MVP feature catalog and dependency order.

Product scope is summarized in [docs/product](../product/README.md), and delivery sequencing and quality targets are in [docs/planning](../planning/README.md).

The first implementation slice is [FEAT-001 Reliable Checkout and Cloud Recovery](FEAT-001-offline-checkout/README.md). The remaining platform and product capabilities are documented as planned feature briefs so implementation can proceed deliberately rather than creating undocumented infrastructure or cross-cutting behavior.

## Definition of Ready

A feature is ready for implementation when its folder contains:

```text
README.md
acceptance-criteria.md
api-and-events.md
qa-test-plan.md
rollout.md
```

`README.md` defines the outcome, scope, dependencies, and ownership. The other documents define testable behavior, contracts, quality evidence, deployment, feature flags, rollback, and operational ownership. A planning brief may start with only `README.md`, but it must be completed before the implementation prompt is used.

## Feature folder convention

```text
docs/features/<feature-id>-<short-name>/
├── README.md              # feature brief and status
├── acceptance-criteria.md # testable behavior
├── api-and-events.md      # contracts changed or introduced
└── rollout.md             # flags, migration, deployment, and rollback
```

Use a stable identifier such as `FEAT-001-offline-checkout` or a GitHub issue number. Keep the feature folder focused on one user outcome. Link related ADRs, issues, pull requests, and test plans from the feature README.

## Feature lifecycle

1. Create the feature folder and copy the template below.
2. Define the user outcome, scope, non-goals, dependencies, and acceptance criteria.
3. Review architecture, security, data, offline, payment, and mobile implications.
4. Add API and event contract changes before implementation.
5. Decide whether a feature flag, migration, or rollout plan is required.
6. Use the `Implement RetailPulse feature` prompt to build the vertical slice.
7. Use the `RetailPulse QA review` prompt before merge.
8. Update the feature status and link the merged pull request.
9. Remove temporary flags and mark the feature complete after rollout.

## Template

Create a folder under `docs/features/` and copy `feature-template.md` into it as `README.md`. Copy the supporting files from `templates/`, tailor them to the feature, and add the feature to [ROADMAP.md](ROADMAP.md) before implementation begins.
