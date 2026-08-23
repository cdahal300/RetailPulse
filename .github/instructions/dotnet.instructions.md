---
name: RetailPulse .NET conventions
description: Apply to C# and .NET source and test files in RetailPulse POS.
applyTo: "src/**/*.cs,tests/**/*.cs"
---

- Keep domain logic independent of Azure SDKs, UI frameworks, and database implementations.
- Use async APIs for I/O and pass cancellation tokens through application and infrastructure layers.
- Use explicit result types or domain errors for expected checkout failures; do not use exceptions for normal payment declines.
- Persist sale and outbox records in one local transaction at the edge.
- Include idempotency keys on all sync and payment-provider commands.
- Keep public event payloads versioned and backward compatible.
