# Make the Store Edge the Transaction Authority

* Status: Proposed
* Deciders: TBD
* Date: 2026-08-22

## Context and Problem Statement

A retail store needs checkout to remain responsive and recover safely during temporary cloud or network failures. RetailPulse should not assume that card authorization can occur offline; that capability depends on the selected certified terminal and payment provider.

## Decision Drivers

- OFF-001: Checkout state, product lookup, and cart recovery should remain local when the cloud is unavailable.
- DUR-001: Sale, payment reference, inventory movement, receipt intent, and outbox record must commit atomically.
- SYN-001: Synchronization must tolerate retry, timeout, restart, duplicate delivery, and conflict.
- VIS-001: Operators must see pending synchronization and failure status.

## Considered Options

### Option 1: Local resilience with SQLite and durable outbox

Pros: supports local durability, fast recovery, safe retry, and eventual cloud coordination without requiring certified offline card authorization. Cons: requires local storage management and operational recovery.

### Option 2: Cloud authority with an offline cart only

Pros: simpler consistency model. Cons: cart recovery and cloud outage handling are weaker, and checkout depends directly on every cloud request.

### Option 3: Peer-to-peer store terminals

Pros: may reduce dependence on one edge process. Cons: increases distributed coordination and conflict complexity beyond the MVP.

## Decision

The local store edge owns local checkout state, device integration, cart recovery, and durable retry state. SQLite stores local transaction state. A durable outbox records versioned domain events in the same local transaction as locally accepted business effects. A sync agent sends idempotent commands and events to the cloud, retries transient failures with backoff, records attempts, and applies explicit conflict rules. Payment authorization remains external and must return a valid provider result before RetailPulse marks a sale paid. Cloud read models become authoritative for coordinated cross-store reporting after accepted synchronization.

## Positive Consequences

- POS-001: POS state and recovery remain available during temporary cloud or network outages.
- POS-002: Atomic local persistence prevents a committed sale from lacking its required outbox record.
- POS-003: Restart and reconnect recovery are observable and repeatable.

## Negative Consequences

- NEG-001: The system is eventually consistent between edge and cloud.
- NEG-002: Duplicate delivery and conflict resolution require durable identifiers and policy.
- NEG-003: Local storage capacity, corruption, backup, and device replacement need runbooks.

## Failure Semantics

- A payment approval without a successful local commit is treated as an exceptional recovery case and must be reconciled with the provider reference.
- A local commit succeeds after a valid payment-provider result even when cloud sync is unavailable; the sale is marked sync-pending.
- Transient sync failures retry; permanent validation or authorization failures are quarantined for operator action.
- Duplicate commands are acknowledged without creating duplicate business effects.

## References

- [Architecture overview](../overview.md)
- [FEAT-001 Reliable Checkout and Cloud Recovery](../../features/FEAT-001-offline-checkout/README.md)
