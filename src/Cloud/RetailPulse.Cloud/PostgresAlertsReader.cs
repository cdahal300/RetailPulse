using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PostgresAlertsReader(string? connectionString) : IAlertsReader
{
    private readonly string? connectionString = connectionString;

    public async Task<IReadOnlyList<OperationalAlert>> GetAlertsAsync(TenantStoreScope scope, CancellationToken cancellationToken = default)
    {
        scope.Validate();
        if (string.IsNullOrWhiteSpace(connectionString)) return [];
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 'low-stock:' || b.product_id, b.tenant_id, b.store_id, 'Warning', 'LowStock', 'Low stock: ' || b.product_id, 'Available quantity is ' || b.quantity || ' and the threshold is ' || t.minimum_quantity || '.', CURRENT_TIMESTAMP FROM inventory_balances b JOIN inventory_thresholds t ON t.tenant_id = b.tenant_id AND t.store_id = b.store_id AND t.product_id = b.product_id WHERE b.tenant_id = @tenant AND b.store_id = @store AND b.quantity <= t.minimum_quantity UNION ALL SELECT 'sync:' || message_id, tenant_id, store_id, 'Critical', 'SyncFailure', 'Synchronization requires attention', 'Delivery status is ' || status || '.', COALESCE(last_attempt_at, occurred_at) FROM sync_delivery_status WHERE tenant_id = @tenant AND store_id = @store AND status IN ('Retry', 'Review', 'DeadLetter') ORDER BY 7 DESC;";
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("store", scope.StoreId);
        var results = new List<OperationalAlert>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)));
        }
        return results;
    }

    public async Task<NotificationPreferences> GetPreferencesAsync(TenantStoreScope scope, string subjectId, CancellationToken cancellationToken = default)
    {
        scope.Validate();
        if (string.IsNullOrWhiteSpace(connectionString)) return DefaultPreferences(scope, subjectId);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT low_stock_enabled, sync_failure_enabled, updated_at FROM notification_preferences WHERE tenant_id = @tenant AND store_id = @store AND subject_id = @subject;";
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("store", scope.StoreId);
        command.Parameters.AddWithValue("subject", subjectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(scope.TenantId, scope.StoreId, subjectId, reader.GetBoolean(0), reader.GetBoolean(1), reader.GetFieldValue<DateTimeOffset>(2))
            : DefaultPreferences(scope, subjectId);
    }

    public async Task<NotificationPreferences> SetPreferencesAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return preferences;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO notification_preferences (tenant_id, store_id, subject_id, low_stock_enabled, sync_failure_enabled, updated_at) VALUES (@tenant, @store, @subject, @low, @sync, @updated) ON CONFLICT (tenant_id, store_id, subject_id) DO UPDATE SET low_stock_enabled = EXCLUDED.low_stock_enabled, sync_failure_enabled = EXCLUDED.sync_failure_enabled, updated_at = EXCLUDED.updated_at;";
        command.Parameters.AddWithValue("tenant", preferences.TenantId);
        command.Parameters.AddWithValue("store", preferences.StoreId);
        command.Parameters.AddWithValue("subject", preferences.SubjectId);
        command.Parameters.AddWithValue("low", preferences.LowStockEnabled);
        command.Parameters.AddWithValue("sync", preferences.SyncFailureEnabled);
        command.Parameters.AddWithValue("updated", preferences.UpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return preferences;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        await command.ExecuteScalarAsync(cancellationToken);
        return connection;
    }

    private static NotificationPreferences DefaultPreferences(TenantStoreScope scope, string subjectId) => new(scope.TenantId, scope.StoreId, subjectId, true, true, DateTimeOffset.UtcNow);
}
