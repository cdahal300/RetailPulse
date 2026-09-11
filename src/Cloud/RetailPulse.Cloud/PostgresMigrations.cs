using Npgsql;

namespace RetailPulse.Cloud;

public static class PostgresMigrations
{
    public const int CurrentVersion = 1;

    public static async Task ApplyAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(482901); CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL);";
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        var currentVersion = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (currentVersion < 1)
        {
            command.CommandText = "CREATE TABLE IF NOT EXISTS inventory_balances (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, product_id TEXT NOT NULL, quantity INTEGER NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id, product_id)); CREATE TABLE IF NOT EXISTS inventory_movements (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, movement_id TEXT NOT NULL, product_id TEXT NOT NULL, quantity_delta INTEGER NOT NULL, reason TEXT NOT NULL, effective_at TIMESTAMPTZ NOT NULL, expected_version INTEGER NOT NULL, aggregate_version INTEGER NOT NULL, actor_id TEXT NOT NULL, role TEXT NOT NULL, command_id TEXT NOT NULL, correlation_id TEXT NOT NULL, PRIMARY KEY (tenant_id, store_id, movement_id), UNIQUE (tenant_id, store_id, command_id)); CREATE TABLE IF NOT EXISTS inventory_thresholds (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, product_id TEXT NOT NULL, minimum_quantity INTEGER NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id, product_id)); CREATE TABLE IF NOT EXISTS sync_delivery_status (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, message_id TEXT NOT NULL, status TEXT NOT NULL, occurred_at TIMESTAMPTZ NOT NULL, last_attempt_at TIMESTAMPTZ NULL, PRIMARY KEY (tenant_id, store_id, message_id)); CREATE INDEX IF NOT EXISTS ix_sync_delivery_scope_status ON sync_delivery_status (tenant_id, store_id, status); CREATE TABLE IF NOT EXISTS notification_preferences (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, subject_id TEXT NOT NULL, low_stock_enabled BOOLEAN NOT NULL, sync_failure_enabled BOOLEAN NOT NULL, updated_at TIMESTAMPTZ NOT NULL, PRIMARY KEY (tenant_id, store_id, subject_id)); CREATE TABLE IF NOT EXISTS push_subscriptions (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, subject_id TEXT NOT NULL, endpoint TEXT NOT NULL, p256dh TEXT NOT NULL, auth TEXT NOT NULL, updated_at TIMESTAMPTZ NOT NULL, PRIMARY KEY (tenant_id, store_id, subject_id, endpoint)); CREATE TABLE IF NOT EXISTS store_settings (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, display_name TEXT NOT NULL, time_zone TEXT NOT NULL, currency TEXT NOT NULL, inventory_adjustments_enabled BOOLEAN NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id)); CREATE TABLE IF NOT EXISTS identity_audit_events (event_id TEXT PRIMARY KEY, aggregate_id TEXT NOT NULL, tenant_id TEXT NOT NULL, store_id TEXT NULL, occurred_at TIMESTAMPTZ NOT NULL, correlation_id TEXT NOT NULL, subject_id TEXT NULL, action TEXT NOT NULL, outcome TEXT NOT NULL, failure TEXT NULL, schema_version INTEGER NOT NULL); CREATE TABLE IF NOT EXISTS revoked_identity_subjects (tenant_id TEXT NOT NULL, subject_id TEXT NOT NULL, revoked_at TIMESTAMPTZ NOT NULL, PRIMARY KEY (tenant_id, subject_id)); CREATE TABLE IF NOT EXISTS revoked_identity_tokens (token_id TEXT PRIMARY KEY, revoked_at TIMESTAMPTZ NOT NULL); CREATE TABLE IF NOT EXISTS identity_devices (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, device_id TEXT NOT NULL, version INTEGER NOT NULL, revoked BOOLEAN NOT NULL, PRIMARY KEY (tenant_id, store_id, device_id)); CREATE TABLE IF NOT EXISTS identity_device_commands (command_id TEXT PRIMARY KEY, command_type TEXT NOT NULL, outcome TEXT NOT NULL, event_json TEXT NULL); CREATE TABLE IF NOT EXISTS identity_role_assignments (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, subject_id TEXT NOT NULL, roles_json TEXT NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id, subject_id)); CREATE TABLE IF NOT EXISTS identity_role_commands (command_id TEXT PRIMARY KEY, outcome TEXT NOT NULL, event_json TEXT NULL); INSERT INTO schema_migrations (version, applied_at) VALUES (1, CURRENT_TIMESTAMP);";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
