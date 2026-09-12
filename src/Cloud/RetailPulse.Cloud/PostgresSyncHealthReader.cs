using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public interface ISyncHealthReader
{
    Task<SyncHealth> GetAsync(TenantStoreScope scope, CancellationToken cancellationToken = default);
}

public sealed class PostgresSyncHealthReader(string? connectionString) : ISyncHealthReader
{
    private readonly string? connectionString = connectionString;

    public async Task<SyncHealth> GetAsync(TenantStoreScope scope, CancellationToken cancellationToken = default)
    {
        scope.Validate();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new(0, null, null, 0, 0, 0);
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await PostgresScope.SetAsync(connection, null, scope.TenantId, scope.StoreId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FILTER (WHERE status IN ('Pending', 'Retry', 'InFlight')), MIN(occurred_at) FILTER (WHERE status IN ('Pending', 'Retry', 'InFlight')), MAX(last_attempt_at) FILTER (WHERE status = 'Synced'), COUNT(*) FILTER (WHERE status = 'Retry'), COUNT(*) FILTER (WHERE status = 'Review'), COUNT(*) FILTER (WHERE status = 'DeadLetter') FROM sync_delivery_status WHERE tenant_id = @tenant AND store_id = @store;";
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("store", scope.StoreId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new(0, null, null, 0, 0, 0);
        }

        return new(
            reader.GetInt64(0) is var pending ? checked((int)pending) : 0,
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            checked((int)reader.GetInt64(3)),
            checked((int)reader.GetInt64(4)),
            checked((int)reader.GetInt64(5)));
    }

}
