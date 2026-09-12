using System.Text.Json;
using Microsoft.Data.Sqlite;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Edge;

public sealed class SqliteFeatureFlagSnapshotStore : IFeatureFlagSnapshotStore
{
    private readonly string connectionString;

    public SqliteFeatureFlagSnapshotStore(string databasePath)
    {
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        Initialize();
    }

    public async Task<FeatureFlagSnapshot?> GetSnapshotAsync(FeatureFlagContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = await ReadAsync(ScopeKey(context.TenantId, context.StoreId), cancellationToken);
        return snapshot ?? await ReadAsync(ScopeKey(context.TenantId, null), cancellationToken);
    }

    public async Task<bool> PublishSnapshotAsync(FeatureFlagSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshot.TenantId) || snapshot.Version <= 0 || snapshot.ExpiresAt == default)
        {
            throw new ArgumentException("Snapshot tenant, positive version, and expiry are required.", nameof(snapshot));
        }

        await using var connection = await OpenAsync(cancellationToken);
    using var transaction = connection.BeginTransaction();
        var scopeKey = ScopeKey(snapshot.TenantId, snapshot.StoreId);
        await using var existingCommand = connection.CreateCommand();
        existingCommand.Transaction = transaction;
        existingCommand.CommandText = "SELECT version FROM feature_flag_snapshots WHERE scope_key = $scopeKey;";
        existingCommand.Parameters.AddWithValue("$scopeKey", scopeKey);
        var existing = await existingCommand.ExecuteScalarAsync(cancellationToken);
        if (existing is long currentVersion && snapshot.Version <= currentVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO feature_flag_snapshots (scope_key, version, snapshot_json)
            VALUES ($scopeKey, $version, $snapshotJson)
            ON CONFLICT(scope_key) DO UPDATE SET version = excluded.version, snapshot_json = excluded.snapshot_json;
            """;
        command.Parameters.AddWithValue("$scopeKey", scopeKey);
        command.Parameters.AddWithValue("$version", snapshot.Version);
        command.Parameters.AddWithValue("$snapshotJson", JsonSerializer.Serialize(snapshot));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<FeatureFlagSnapshot?> ReadAsync(string scopeKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT snapshot_json FROM feature_flag_snapshots WHERE scope_key = $scopeKey;";
        command.Parameters.AddWithValue("$scopeKey", scopeKey);
        var serialized = await command.ExecuteScalarAsync(cancellationToken) as string;
        return serialized is null ? null : JsonSerializer.Deserialize<FeatureFlagSnapshot>(serialized);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS feature_flag_snapshots (
                scope_key TEXT PRIMARY KEY,
                version INTEGER NOT NULL,
                snapshot_json TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static string ScopeKey(string tenantId, string? storeId) => $"{tenantId}|{storeId}";
}