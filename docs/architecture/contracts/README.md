# Contracts

This directory is the central home for cross-boundary contract references and generated contract artifacts.

## Locations

- OpenAPI: cloud and edge HTTP API specifications, location TBD.
- AsyncAPI: event channels and message schemas, location TBD.
- Domain events: versioned event schemas and examples, location TBD.
- Contract tests: [tests/Contract](../../../tests/Contract/).

## Version Policy

Every public API or event contract must have an explicit version. Additive, backward-compatible changes are preferred. Breaking changes require a new version, migration or dual-read/write plan, consumer validation, and deprecation timeline. Event envelopes include event ID, aggregate ID, store ID, occurred time, and schema version. The canonical repository format and publishing pipeline are decision required.
