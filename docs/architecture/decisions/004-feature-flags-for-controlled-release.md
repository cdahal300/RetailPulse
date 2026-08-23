# ADR 004: Feature Flags for Controlled Release

## Status

Accepted

## Context

Retail stores cannot always absorb a risky release at the same time. The POS also has an offline edge runtime, so a feature rollout must be safe when a store temporarily cannot reach the cloud. Deployment and activation need to be independently controlled.

## Decision

Use a shared feature-flag abstraction with Azure App Configuration Feature Management as the initial provider. Evaluate flags on the server and distribute an authenticated, locally cached snapshot to store-edge runtimes. Use stable targeting by environment, store, terminal, role, and percentage where appropriate.

All flags require an owner, description, safe default, risk classification, audit history, and expiry date. New flags are off by default. Production activation requires a separate approval from deployment.

## Consequences

- Features can be enabled for a pilot store without redeploying the application.
- Rollback can usually disable behavior without reverting a container image.
- The edge can continue operating with its last known snapshot and deterministic defaults.
- The system must manage stale snapshots, flag cleanup, audit permissions, and targeting mistakes.
- Business code depends on the internal abstraction rather than directly on Azure App Configuration, preserving an option to use OpenFeature-compatible providers such as Unleash later.

## Out of scope

Feature flags do not replace authentication, authorization, payment controls, database migrations, or emergency security controls. Schema changes must remain backward compatible with both flag states during rollout.