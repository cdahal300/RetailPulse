using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PostgresStoreSettingsRepository(string? connectionString) : IStoreSettingsRepository
{
    private readonly string? connectionString = connectionString;

    public async Task<StoreSettings> GetAsync(TenantStoreScope scope, CancellationToken cancellationToken = default)
    {
        scope.Validate();
        if (string.IsNullOrWhiteSpace(connectionString)) return Defaults(scope);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT display_name, time_zone, currency, inventory_adjustments_enabled, version FROM store_settings WHERE tenant_id = @tenant AND store_id = @store;";
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("store", scope.StoreId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(scope.TenantId, scope.StoreId, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetInt32(4)) : Defaults(scope);
    }

    public async Task<StoreSettings?> UpdateAsync(StoreSettings settings, int expectedVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return expectedVersion == settings.Version ? settings with { Version = expectedVersion + 1 } : null;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO store_settings (tenant_id, store_id, display_name, time_zone, currency, inventory_adjustments_enabled, version) VALUES (@tenant, @store, @name, @timezone, @currency, @enabled, @version) ON CONFLICT (tenant_id, store_id) DO UPDATE SET display_name = EXCLUDED.display_name, time_zone = EXCLUDED.time_zone, currency = EXCLUDED.currency, inventory_adjustments_enabled = EXCLUDED.inventory_adjustments_enabled, version = EXCLUDED.version WHERE store_settings.version = @expected RETURNING version;";
        command.Parameters.AddWithValue("tenant", settings.TenantId);
        command.Parameters.AddWithValue("store", settings.StoreId);
        command.Parameters.AddWithValue("name", settings.DisplayName);
        command.Parameters.AddWithValue("timezone", settings.TimeZone);
        command.Parameters.AddWithValue("currency", settings.Currency);
        command.Parameters.AddWithValue("enabled", settings.InventoryAdjustmentsEnabled);
        command.Parameters.AddWithValue("version", expectedVersion + 1);
        command.Parameters.AddWithValue("expected", expectedVersion);
        return await command.ExecuteScalarAsync(cancellationToken) is null ? null : settings with { Version = expectedVersion + 1 };
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS store_settings (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, display_name TEXT NOT NULL, time_zone TEXT NOT NULL, currency TEXT NOT NULL, inventory_adjustments_enabled BOOLEAN NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id));";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static StoreSettings Defaults(TenantStoreScope scope) => new(scope.TenantId, scope.StoreId, scope.StoreId == "store-1" ? "Bardstown Road" : "South End Market", "UTC", "USD", true, 0);
}