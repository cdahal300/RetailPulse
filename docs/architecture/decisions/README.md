# Architecture Decision Records

ADRs capture durable architecture decisions, their drivers, alternatives, and consequences. They supplement [overview.md](../overview.md); they do not replace it.

## Index

| ADR | Decision | Status |
|---|---|---|
| [001](001-use-aks.md) | Use AKS as the application host | Proposed |
| [002](002-offline-first-edge.md) | Make the store edge the transaction authority | Proposed |
| [003](003-external-payment-provider.md) | Keep payment processing external | Proposed |
| [004](004-feature-flags-for-controlled-release.md) | Feature flags for controlled release | Accepted |

## Writing Convention

- Name files `NNN-short-title.md` using the next sequential number.
- Use a clear title, status, deciders, and `YYYY-MM-DD` date.
- Include context, decision drivers, considered options, decision, positive consequences, negative consequences, implementation notes, and references.
- Mark unresolved business input as `TBD` or `decision required`.
- Keep decisions stable and update status or supersession explicitly rather than rewriting history.
