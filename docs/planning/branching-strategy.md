# Branching Strategy

## Decision

Use trunk-based development with a protected `main` branch. Keep branches short-lived and merge through pull requests. Do not create a long-lived `develop` branch for the MVP.

```text
feature branch -> pull request -> main -> development deployment
                                      |
                                      +-> staging approval
                                      |
                                      +-> production approval
                                      |
                                      +-> feature-flag activation
```

## Branch naming

Use lowercase names with the feature or work identifier:

```text
feat/FEAT-002-sqlite-persistence
fix/FEAT-001-payment-timeout
chore/ci-pipeline
docs/architecture-update
infra/FEAT-006-aks-foundation
qa/FEAT-003-duplicate-delivery
```

Avoid personal names, vague names such as `changes`, and branches that live across multiple unrelated features.

## Pull request rules

- Every change enters `main` through a pull request.
- Link the pull request to its feature specification and issue.
- Keep pull requests small enough to review as one vertical slice.
- Required CI checks must pass: .NET build/tests, PWA lint/build, and secret scan.
- At least one code owner approves normal changes.
- Require the relevant owner for payment, tenant isolation, infrastructure, CI/CD, observability, or AI data changes.
- Branches must be up to date with `main` before merge when conflicts or contract changes exist.
- Prefer squash merge to keep `main` readable.
- Delete merged branches.

## Work in progress

Use a draft pull request for incomplete work. Keep incomplete behavior behind a disabled feature flag when it must be merged before it is fully enabled. Do not merge knowingly broken code to `main` just to make progress.

## Releases

Use annotated version tags from `main`:

```text
v0.1.0   MVP development milestone
v0.1.1   compatible patch
v0.2.0   compatible feature release
```

Use semantic versioning for externally consumed APIs and events. A release tag identifies the code deployed; feature-flag activation is a separate audited action.

## Hotfixes

Create `fix/` or `hotfix/` from `main`, open a pull request, run the same required CI checks, merge, and tag a patch release when appropriate. Do not maintain a separate production branch until release frequency or compliance requires it.

## Environment promotion

Do not use Git branches to represent development, staging, and production. Promote the same immutable image or commit through environments:

```text
main commit
    -> development
    -> staging approval
    -> production approval
```

Feature flags control exposure by environment, tenant, store, role, or percentage. They do not replace authorization, payment controls, audit requirements, or database migration compatibility.

## Branch protection baseline

Configure GitHub branch protection for `main` with:

- Pull request requirement
- Required CI checks
- At least one approval
- Code-owner review for owned paths
- No force pushes
- No branch deletion
- Conversation resolution before merge
- Optional linear history or squash merging

Exact repository rules are configured in GitHub settings, not committed as secrets or local configuration.
