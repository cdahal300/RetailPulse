# Contributing to RetailPulse

## Pull requests

- Start from a feature specification under `docs/features/`.
- Keep changes focused on one vertical slice.
- Update acceptance criteria, contracts, QA coverage, and rollout notes when behavior changes.
- Add tests for happy path, failure, duplicate, timeout, authorization, and tenant-isolation behavior as applicable.
- Do not commit secrets, credentials, card data, kubeconfig files, or generated build output.
- Require CI to pass before merge.

## Review expectations

At least one code owner must approve changes. Changes to payment boundaries, tenant isolation, infrastructure, CI/CD, or AI data handling require the relevant owner review.

## Commit and branch guidance

Use the [branching strategy](docs/planning/branching-strategy.md). Create short-lived branches from `main`, such as `feat/FEAT-002-sqlite-persistence`, `fix/FEAT-001-payment-timeout`, or `infra/FEAT-006-aks-foundation`. Open a draft pull request for work in progress and a normal pull request when the required checks and review evidence are ready.

Do not create `develop`, `staging`, or `production` branches. Promote the same immutable commit or image through environments. Link the feature specification and issue. Keep deployment and feature activation as separate changes when staged rollout is required.
