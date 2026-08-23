# FEAT-002: Acceptance Criteria

## Functional behavior

- Given a valid sale, when checkout commits, then sale, lines, payment reference, inventory movement, receipt intent, and outbox message are durable in one SQLite transaction.
- Given a process restart or network outage, when the edge starts or reconnects, then committed data and pending outbox work are recoverable without data loss.

## Failure and resilience behavior

- Given a validation, SQLite, or commit failure, when checkout writes, then no partial sale, movement, receipt intent, or outbox message remains.
- Given a full, locked, corrupt, or unavailable database, when the edge writes or starts, then it fails closed, preserves existing data, exposes an actionable health state, and does not claim checkout success.
- Given an interrupted migration, when startup retries, then migration is repeatable and leaves the database at a known schema version.

## Authorization and isolation

- Given a registered edge instance and store identity, when local data is accessed, then only authorized local runtime components can read or mutate that store's database.
- Given an untrusted local request or wrong store context, when persistence is attempted, then it is rejected and audited without cross-store access.

## Data and security

- Sensitive data handling: persist only opaque payment references and approved transaction metadata; never PAN, CVV, PIN, magnetic-stripe, or raw card data.
- Audit requirements: record schema migrations, storage failures, recovery actions, and transaction correlation identifiers.
- Retention and deletion: apply store retention policy to receipts and operational data; delete or archive only through an audited maintenance operation.
- Encrypt the database and backups where supported, restrict file permissions, and exclude database files from logs and telemetry.
