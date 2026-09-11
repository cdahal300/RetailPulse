using System.Globalization;
using Microsoft.Data.Sqlite;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class SqliteInventoryLedger : IInventoryLedgerRepository
{
    private readonly string connectionString;

    public SqliteInventoryLedger(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("A cloud database path is required.", nameof(databasePath));
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            ForeignKeys = true
        }.ToString();
    }

    public async Task<InventoryAppendResult> AppendAsync(InventoryMovementRecord movement, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var existing = await FindMovementAsync(connection, transaction, movement, cancellationToken);
        var current = await ReadBalanceAsync(connection, transaction, movement.TenantId, movement.StoreId, movement.ProductId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(InventoryAppendOutcome.Duplicate, current, existing);
        }

        if (current.Version != movement.ExpectedVersion)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(InventoryAppendOutcome.StaleVersion, current, Error: $"Expected version {movement.ExpectedVersion}, actual version {current.Version}.");
        }

        var quantity = current.Quantity + movement.QuantityDelta;
        if (quantity < 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(InventoryAppendOutcome.NegativeStock, current, Error: "Movement would create negative stock.");
        }

        var balance = current with { Quantity = quantity, Version = current.Version + 1 };
        var insertMovement = CreateCommand(connection, transaction, "INSERT INTO inventory_movements (tenant_id, store_id, movement_id, product_id, quantity_delta, reason, effective_at, expected_version, aggregate_version, actor_id, role, command_id, correlation_id) VALUES ($tenant, $store, $movement, $product, $delta, $reason, $effective, $expected, $aggregate, $actor, $role, $command, $correlation);");
        AddMovementParameters(insertMovement, movement with { AggregateVersion = balance.Version });
        await insertMovement.ExecuteNonQueryAsync(cancellationToken);

        var updateBalance = CreateCommand(connection, transaction, "INSERT INTO inventory_balances (tenant_id, store_id, product_id, quantity, version) VALUES ($tenant, $store, $product, $quantity, $version) ON CONFLICT (tenant_id, store_id, product_id) DO UPDATE SET quantity = excluded.quantity, version = excluded.version;");
        AddScopeParameters(updateBalance, movement.TenantId, movement.StoreId);
        updateBalance.Parameters.AddWithValue("$product", movement.ProductId);
        updateBalance.Parameters.AddWithValue("$quantity", balance.Quantity);
        updateBalance.Parameters.AddWithValue("$version", balance.Version);
        await updateBalance.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(InventoryAppendOutcome.Appended, balance, movement with { AggregateVersion = balance.Version });
    }

    public async Task<InventoryBalance> GetBalanceAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await ReadBalanceAsync(connection, null, scope.TenantId, scope.StoreId, productId, cancellationToken);
    }

    public async Task<InventoryMovementRecord?> FindMovementAsync(CatalogInventoryScope scope, string movementId, string commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = CreateCommand(connection, null, "SELECT tenant_id, store_id, movement_id, product_id, quantity_delta, reason, effective_at, expected_version, aggregate_version, actor_id, role, command_id, correlation_id FROM inventory_movements WHERE tenant_id = $tenant AND store_id = $store AND (movement_id = $movement OR command_id = $command) LIMIT 1;");
        AddScopeParameters(command, scope.TenantId, scope.StoreId);
        command.Parameters.AddWithValue("$movement", movementId);
        command.Parameters.AddWithValue("$command", commandId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMovement(reader) : null;
    }

    public async Task<InventoryThreshold?> GetThresholdAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = CreateCommand(connection, null, "SELECT tenant_id, store_id, product_id, minimum_quantity, version FROM inventory_thresholds WHERE tenant_id = $tenant AND store_id = $store AND product_id = $product;");
        AddScopeParameters(command, scope.TenantId, scope.StoreId);
        command.Parameters.AddWithValue("$product", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4)) : null;
    }

    public async Task SetThresholdAsync(InventoryThreshold threshold, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = CreateCommand(connection, null, "INSERT INTO inventory_thresholds (tenant_id, store_id, product_id, minimum_quantity, version) VALUES ($tenant, $store, $product, $minimum, $version) ON CONFLICT (tenant_id, store_id, product_id) DO UPDATE SET minimum_quantity = excluded.minimum_quantity, version = excluded.version;");
        AddScopeParameters(command, threshold.TenantId, threshold.StoreId);
        command.Parameters.AddWithValue("$product", threshold.ProductId);
        command.Parameters.AddWithValue("$minimum", threshold.MinimumQuantity);
        command.Parameters.AddWithValue("$version", threshold.Version);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000; CREATE TABLE IF NOT EXISTS inventory_balances (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, product_id TEXT NOT NULL, quantity INTEGER NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id, product_id)); CREATE TABLE IF NOT EXISTS inventory_movements (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, movement_id TEXT NOT NULL, product_id TEXT NOT NULL, quantity_delta INTEGER NOT NULL, reason TEXT NOT NULL, effective_at TEXT NOT NULL, expected_version INTEGER NOT NULL, aggregate_version INTEGER NOT NULL, actor_id TEXT NOT NULL, role TEXT NOT NULL, command_id TEXT NOT NULL, correlation_id TEXT NOT NULL, PRIMARY KEY (tenant_id, store_id, movement_id), UNIQUE (tenant_id, store_id, command_id)); CREATE TABLE IF NOT EXISTS inventory_thresholds (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, product_id TEXT NOT NULL, minimum_quantity INTEGER NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id, product_id));";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<InventoryMovementRecord?> FindMovementAsync(SqliteConnection connection, SqliteTransaction transaction, InventoryMovementRecord movement, CancellationToken cancellationToken)
    {
        var command = CreateCommand(connection, transaction, "SELECT tenant_id, store_id, movement_id, product_id, quantity_delta, reason, effective_at, expected_version, aggregate_version, actor_id, role, command_id, correlation_id FROM inventory_movements WHERE tenant_id = $tenant AND store_id = $store AND (movement_id = $movement OR command_id = $command) LIMIT 1;");
        AddScopeParameters(command, movement.TenantId, movement.StoreId);
        command.Parameters.AddWithValue("$movement", movement.MovementId);
        command.Parameters.AddWithValue("$command", movement.CommandId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMovement(reader) : null;
    }

    private static async Task<InventoryBalance> ReadBalanceAsync(SqliteConnection connection, SqliteTransaction? transaction, string tenantId, string storeId, string productId, CancellationToken cancellationToken)
    {
        var command = CreateCommand(connection, transaction, "SELECT quantity, version FROM inventory_balances WHERE tenant_id = $tenant AND store_id = $store AND product_id = $product;");
        AddScopeParameters(command, tenantId, storeId);
        command.Parameters.AddWithValue("$product", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(tenantId, storeId, productId, reader.GetInt32(0), reader.GetInt32(1)) : new(tenantId, storeId, productId, 0, 0);
    }

    private static InventoryMovementRecord ReadMovement(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), Enum.Parse<InventoryMovementReason>(reader.GetString(5)), DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture), reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9), Enum.Parse<CatalogInventoryRole>(reader.GetString(10)), reader.GetString(11), reader.GetString(12));
    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction, string sql) { var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command; }
    private static void AddScopeParameters(SqliteCommand command, string tenantId, string storeId) { command.Parameters.AddWithValue("$tenant", tenantId); command.Parameters.AddWithValue("$store", storeId); }
    private static void AddMovementParameters(SqliteCommand command, InventoryMovementRecord movement) { AddScopeParameters(command, movement.TenantId, movement.StoreId); command.Parameters.AddWithValue("$movement", movement.MovementId); command.Parameters.AddWithValue("$product", movement.ProductId); command.Parameters.AddWithValue("$delta", movement.QuantityDelta); command.Parameters.AddWithValue("$reason", movement.Reason.ToString()); command.Parameters.AddWithValue("$effective", movement.EffectiveAt.ToString("O", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("$expected", movement.ExpectedVersion); command.Parameters.AddWithValue("$aggregate", movement.AggregateVersion); command.Parameters.AddWithValue("$actor", movement.ActorId); command.Parameters.AddWithValue("$role", movement.Role.ToString()); command.Parameters.AddWithValue("$command", movement.CommandId); command.Parameters.AddWithValue("$correlation", movement.CorrelationId); }
}
