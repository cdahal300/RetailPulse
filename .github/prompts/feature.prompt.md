---
name: Implement RetailPulse feature
description: Plan and implement a vertical slice across domain, API, persistence, events, UI, and tests.
agent: RetailPulse Implementer
---

Implement this feature: ${input:feature:Describe the feature}

Start every new feature from a fresh branch created from the latest `main`, using the naming convention `feat/<feature-id>-<short-name>`. Do not implement a new feature directly on `main` or reuse another feature branch.

The feature specification should be in `docs/features/<feature-id>-<short-name>/README.md`. Use the repository architecture and instructions. Before editing, identify the owning module, data changes, event contracts, offline behavior, security implications, feature-flag needs, and nearest tests. Implement the smallest complete vertical slice. Add or update focused tests, then run validation and summarize changed files, risks, and follow-up work.
