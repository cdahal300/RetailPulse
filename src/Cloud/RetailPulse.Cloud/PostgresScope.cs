using Npgsql;

namespace RetailPulse.Cloud;

internal static class PostgresScope
{
    public static async Task SetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string tenantId,
        string? storeId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant, true), set_config('app.store_id', @store, true);";
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("store", (object?)storeId ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
