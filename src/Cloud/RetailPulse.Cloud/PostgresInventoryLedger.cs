using System.Globalization;
using System.Data;
using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PostgresInventoryLedger : IInventoryLedgerRepository
{
    private readonly string connectionString;

    public PostgresInventoryLedger(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException("A PostgreSQL connection string is required.", nameof(connectionString));
        this.connectionString = connectionString;
    }

    public async Task<InventoryAppendResult> AppendAsync(InventoryMovementRecord movement, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
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

        var persisted = movement with { AggregateVersion = current.Version + 1 };
        await ExecuteAsync(connection, transaction, "INSERT INTO inventory_movements (tenant_id, store_id, movement_id, product_id, quantity_delta, reason, effective_at, expected_version, aggregate_version, actor_id, role, command_id, correlation_id) VALUES (@tenant, @store, @movement, @product, @delta, @reason, @effective, @expected, @aggregate, @actor, @role, @command, @correlation);", command => AddMovementParameters(command, persisted), cancellationToken);
        await ExecuteAsync(connection, transaction, "INSERT INTO inventory_balances (tenant_id, store_id, product_id, quantity, version) VALUES (@tenant, @store, @product, @quantity, @version) ON CONFLICT (tenant_id, store_id, product_id) DO UPDATE SET quantity = EXCLUDED.quantity, version = EXCLUDED.version;", command =>
        {
            command.Parameters.AddWithValue("tenant", movement.TenantId);
            command.Parameters.AddWithValue("store", movement.StoreId);
            command.Parameters.AddWithValue("product", movement.ProductId);
            command.Parameters.AddWithValue("quantity", quantity);
            command.Parameters.AddWithValue("version", persisted.AggregateVersion);
        }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(InventoryAppendOutcome.Appended, new(movement.TenantId, movement.StoreId, movement.ProductId, quantity, persisted.AggregateVersion), persisted);
    }

    public async Task<InventoryBalance> GetBalanceAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await ReadBalanceAsync(connection, null, scope.TenantId, scope.StoreId, productId, cancellationToken);
    }

    public async Task<InventoryMovementRecord?> FindMovementAsync(CatalogInventoryScope scope, string movementId, string commandId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, null, "SELECT tenant_id, store_id, movement_id, product_id, quantity_delta, reason, effective_at, expected_version, aggregate_version, actor_id, role, command_id, correlation_id FROM inventory_movements WHERE tenant_id = @tenant AND store_id = @store AND (movement_id = @movement OR command_id = @command) LIMIT 1;");
        AddScopeParameters(command, scope.TenantId, scope.StoreId);
        command.Parameters.AddWithValue("movement", movementId);
        command.Parameters.AddWithValue("command", commandId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMovement(reader) : null;
    }

    public async Task<InventoryThreshold?> GetThresholdAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, null, "SELECT tenant_id, store_id, product_id, minimum_quantity, version FROM inventory_thresholds WHERE tenant_id = @tenant AND store_id = @store AND product_id = @product;");
        AddScopeParameters(command, scope.TenantId, scope.StoreId);
        command.Parameters.AddWithValue("product", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4)) : null;
    }

    public async Task SetThresholdAsync(InventoryThreshold threshold, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, null, "INSERT INTO inventory_thresholds (tenant_id, store_id, product_id, minimum_quantity, version) VALUES (@tenant, @store, @product, @minimum, @version) ON CONFLICT (tenant_id, store_id, product_id) DO UPDATE SET minimum_quantity = EXCLUDED.minimum_quantity, version = EXCLUDED.version;");
        AddScopeParameters(command, threshold.TenantId, threshold.StoreId);
        command.Parameters.AddWithValue("product", threshold.ProductId);
        command.Parameters.AddWithValue("minimum", threshold.MinimumQuantity);
        command.Parameters.AddWithValue("version", threshold.Version);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }


    private static async Task<InventoryMovementRecord?> FindMovementAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, InventoryMovementRecord movement, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT tenant_id, store_id, movement_id, product_id, quantity_delta, reason, effective_at, expected_version, aggregate_version, actor_id, role, command_id, correlation_id FROM inventory_movements WHERE tenant_id = @tenant AND store_id = @store AND (movement_id = @movement OR command_id = @command) LIMIT 1;");
        AddScopeParameters(command, movement.TenantId, movement.StoreId);
        command.Parameters.AddWithValue("movement", movement.MovementId);
        command.Parameters.AddWithValue("command", movement.CommandId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMovement(reader) : null;
    }

    private static async Task<InventoryBalance> ReadBalanceAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string tenantId, string storeId, string productId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT quantity, version FROM inventory_balances WHERE tenant_id = @tenant AND store_id = @store AND product_id = @product;");
        AddScopeParameters(command, tenantId, storeId);
        command.Parameters.AddWithValue("product", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(tenantId, storeId, productId, reader.GetInt32(0), reader.GetInt32(1)) : new(tenantId, storeId, productId, 0, 0);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Action<NpgsqlCommand> addParameters, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, sql);
        addParameters(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static InventoryMovementRecord ReadMovement(NpgsqlDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), Enum.Parse<InventoryMovementReason>(reader.GetString(5)), reader.GetFieldValue<DateTimeOffset>(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9), Enum.Parse<CatalogInventoryRole>(reader.GetString(10)), reader.GetString(11), reader.GetString(12));
    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql) { var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command; }
    private static void AddScopeParameters(NpgsqlCommand command, string tenantId, string storeId) { command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("store", storeId); }
    private static void AddMovementParameters(NpgsqlCommand command, InventoryMovementRecord movement) { AddScopeParameters(command, movement.TenantId, movement.StoreId); command.Parameters.AddWithValue("movement", movement.MovementId); command.Parameters.AddWithValue("product", movement.ProductId); command.Parameters.AddWithValue("delta", movement.QuantityDelta); command.Parameters.AddWithValue("reason", movement.Reason.ToString()); command.Parameters.AddWithValue("effective", movement.EffectiveAt); command.Parameters.AddWithValue("expected", movement.ExpectedVersion); command.Parameters.AddWithValue("aggregate", movement.AggregateVersion); command.Parameters.AddWithValue("actor", movement.ActorId); command.Parameters.AddWithValue("role", movement.Role.ToString()); command.Parameters.AddWithValue("command", movement.CommandId); command.Parameters.AddWithValue("correlation", movement.CorrelationId); }
}
