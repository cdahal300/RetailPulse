# ADR 005: Multi-Tenant and Store Isolation

## Status

Accepted

## Context

RetailPulse serves multiple retail tenants, each with one or more stores and registered checkout devices. A single AKS platform, event bus, analytics platform, and PWA may serve multiple tenants. A client-controlled tenant or store filter is not a security boundary.

## Decision

Resolve tenant and store scope from validated identity claims, device registration, and server-side authorization policy. Apply that scope to every request, command, event, database operation, cache key, analytics partition, export, feature-flag evaluation, AI job, audit record, and operational view.

Use defense in depth: application authorization plus repository-level scope enforcement, with PostgreSQL row-level security or an equivalent database policy where practical. Consumers and workers revalidate scope. Cross-tenant access is denied by default and treated as a release-blocking security defect.

## Consequences

- One deployment can safely serve multiple tenants when scope is consistently enforced.
- Tenant and store context must be present in domain contracts and test fixtures.
- Cache keys and analytics partitions require careful design to prevent data bleed.
- Cross-tenant reporting requires an explicitly authorized operator boundary and separate audit trail; it is not part of normal tenant access.
- Data retention, deletion, export, backup, and restore procedures must preserve tenant boundaries.

## Out of scope

This decision does not define commercial tenant onboarding, billing, cross-tenant benchmarking, or regulatory retention periods. Those remain product and legal decisions.