# RetailPulse development instructions

## Architecture

- Treat `docs/architecture/overview.md` and `docs/architecture/` as the architecture source of truth.
- Preserve the offline-first edge boundary. Checkout must not require a cloud round trip.
- Keep payment processing external. Never persist PAN, CVV, PIN, magnetic-stripe data, or raw card data.
- Use domain events for cross-module side effects and include event ID, aggregate ID, store ID, occurred time, and schema version.
- Prefer a modular monolith for the MVP. Extract services only when ownership, scale, or release cadence requires it.

## Implementation

- Use C# and .NET for edge services, cloud APIs, workers, and provider adapters.
- Use SQLite for local edge persistence and PostgreSQL for cloud persistence.
- Use dependency injection and interfaces for payment, sync transport, identity, and AI providers.
- Make cloud commands idempotent and make retries safe.
- Put new behavior behind a feature flag when it needs staged rollout; default flags to the safest behavior.
- Never use feature flags to bypass authorization, payment-provider controls, or audit requirements.
- Keep secrets in environment variables locally and Azure Key Vault in deployed environments.

## Quality

- Add unit tests for domain rules, integration tests for persistence and messaging, and contract tests for events.
- Test offline, retry, duplicate delivery, timeout, and conflict scenarios explicitly.
- Use structured logs, correlation IDs, metrics, and OpenTelemetry traces.
- Do not add AI to a critical transaction path. Validate AI output against a schema and retain an audit record.

## Workflow

Before editing, identify the owning module and its nearest test. Make the smallest change that proves the behavior. Run the narrowest relevant test first, then the broader suite before merging.

Every new feature must start on a fresh branch from the latest `main`, named `feat/<feature-id>-<short-name>`. Do not implement new features directly on `main` or reuse another feature branch. Merge through a pull request after required CI and review checks pass.
