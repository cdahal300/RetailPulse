using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PostgresPushSubscriptionStore(string? connectionString) : IPushSubscriptionStore
{
    private readonly string? connectionString = connectionString;

    public async Task RegisterAsync(PushSubscription subscription, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO push_subscriptions (tenant_id, store_id, subject_id, endpoint, p256dh, auth, updated_at) VALUES (@tenant, @store, @subject, @endpoint, @p256dh, @auth, @updated) ON CONFLICT (tenant_id, store_id, subject_id, endpoint) DO UPDATE SET p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth, updated_at = EXCLUDED.updated_at;";
        command.Parameters.AddWithValue("tenant", subscription.TenantId);
        command.Parameters.AddWithValue("store", subscription.StoreId);
        command.Parameters.AddWithValue("subject", subscription.SubjectId);
        command.Parameters.AddWithValue("endpoint", subscription.Endpoint);
        command.Parameters.AddWithValue("p256dh", subscription.P256dh);
        command.Parameters.AddWithValue("auth", subscription.Auth);
        command.Parameters.AddWithValue("updated", subscription.UpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAsync(TenantStoreScope scope, string subjectId, string endpoint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM push_subscriptions WHERE tenant_id = @tenant AND store_id = @store AND subject_id = @subject AND endpoint = @endpoint;";
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("store", scope.StoreId);
        command.Parameters.AddWithValue("subject", subjectId);
        command.Parameters.AddWithValue("endpoint", endpoint);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
}
