using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Edge;

public sealed record PersistenceHealth(int SchemaVersion, bool Available, int PendingOutboxCount, string? LastRecoveryError);

public sealed class SqliteCheckoutPersistence : ILocalCheckoutPersistence, IAsyncDisposable
{
    public const int CurrentSchemaVersion = 1;

    private readonly string connectionString;
    private string? lastRecoveryError;

    public SqliteCheckoutPersistence(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A SQLite database path is required.", nameof(databasePath));
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            ForeignKeys = true
        }.ToString();
    }

    public async Task CommitAsync(CheckoutCommit commit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ValidateScope(commit);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await ExistsAsync(connection, transaction, commit, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            await InsertSaleAsync(connection, transaction, commit.Sale, cancellationToken);
            await InsertSaleLinesAsync(connection, transaction, commit.Sale, cancellationToken);
            await InsertPaymentAsync(connection, transaction, commit, cancellationToken);
            await InsertInventoryAsync(connection, transaction, commit, cancellationToken);
            await InsertReceiptAsync(connection, transaction, commit, cancellationToken);
            await InsertOutboxAsync(connection, transaction, commit, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            lastRecoveryError = null;
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            lastRecoveryError = exception.Message;
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PersistenceHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            var pending = connection.CreateCommand();
            pending.CommandText = "SELECT COUNT(*) FROM outbox_messages WHERE status = 'Pending';";
            var pendingCount = Convert.ToInt32(await pending.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            return new(CurrentSchemaVersion, true, pendingCount, lastRecoveryError);
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            lastRecoveryError = exception.Message;
            return new(0, false, 0, lastRecoveryError);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await ApplyMigrationsAsync(connection, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task ApplyMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var migrationTransaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.Transaction = migrationTransaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_version (id INTEGER PRIMARY KEY CHECK (id = 1), version INTEGER NOT NULL);
            INSERT INTO schema_version (id, version) SELECT 1, 0 WHERE NOT EXISTS (SELECT 1 FROM schema_version WHERE id = 1);
            CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var version = await ReadSchemaVersionAsync(connection, cancellationToken);
        if (version < 1)
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS sales (
                    sale_id TEXT PRIMARY KEY, tenant_id TEXT NOT NULL, store_id TEXT NOT NULL,
                    terminal_id TEXT NOT NULL, local_transaction_id TEXT NOT NULL,
                    subtotal_minor INTEGER NOT NULL, tax_minor INTEGER NOT NULL, discount_minor INTEGER NOT NULL,
                    total_minor INTEGER NOT NULL, currency TEXT NOT NULL, state TEXT NOT NULL, occurred_at TEXT NOT NULL,
                    UNIQUE (tenant_id, store_id, terminal_id, local_transaction_id)
                );
                CREATE TABLE IF NOT EXISTS sale_lines (
                    tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, sale_id TEXT NOT NULL, line_number INTEGER NOT NULL,
                    product_id TEXT NOT NULL CHECK (length(product_id) > 0), product_name TEXT NOT NULL, quantity INTEGER NOT NULL CHECK (quantity > 0),
                    unit_price_minor INTEGER NOT NULL, currency TEXT NOT NULL, tax_category TEXT NOT NULL,
                    PRIMARY KEY (tenant_id, store_id, sale_id, line_number),
                    FOREIGN KEY (sale_id) REFERENCES sales (sale_id)
                );
                CREATE TABLE IF NOT EXISTS payment_captures (
                    tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, sale_id TEXT NOT NULL,
                    status TEXT NOT NULL, provider_reference TEXT NOT NULL, authorization_code TEXT,
                    PRIMARY KEY (tenant_id, store_id, sale_id), FOREIGN KEY (sale_id) REFERENCES sales (sale_id)
                );
                CREATE TABLE IF NOT EXISTS inventory_movements (
                    tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, sale_id TEXT NOT NULL, movement_number INTEGER NOT NULL,
                    product_id TEXT NOT NULL CHECK (length(product_id) > 0), quantity_delta INTEGER NOT NULL,
                    PRIMARY KEY (tenant_id, store_id, sale_id, movement_number), FOREIGN KEY (sale_id) REFERENCES sales (sale_id)
                );
                CREATE TABLE IF NOT EXISTS receipt_intents (
                    tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, sale_id TEXT NOT NULL,
                    PRIMARY KEY (tenant_id, store_id, sale_id), FOREIGN KEY (sale_id) REFERENCES sales (sale_id)
                );
                CREATE TABLE IF NOT EXISTS outbox_messages (
                    message_id TEXT PRIMARY KEY, tenant_id TEXT NOT NULL, store_id TEXT NOT NULL,
                    idempotency_key TEXT NOT NULL UNIQUE, message_type TEXT NOT NULL, occurred_at TEXT NOT NULL,
                    schema_version INTEGER NOT NULL, payload_json TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'Pending',
                    attempt_count INTEGER NOT NULL DEFAULT 0, last_attempt_at TEXT, last_error TEXT
                );
                CREATE TABLE IF NOT EXISTS sync_attempts (
                    message_id TEXT NOT NULL, tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, attempted_at TEXT NOT NULL,
                    succeeded INTEGER NOT NULL, error TEXT, FOREIGN KEY (message_id) REFERENCES outbox_messages (message_id)
                );
                CREATE INDEX IF NOT EXISTS ix_outbox_pending ON outbox_messages (tenant_id, store_id, status, occurred_at);
                INSERT INTO schema_migrations (version, applied_at) VALUES (1, $appliedAt);
                UPDATE schema_version SET version = 1 WHERE id = 1;
                """;
            command.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await migrationTransaction.CommitAsync(cancellationToken);
    }

    private static async Task<int> ReadSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version WHERE id = 1;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> ExistsAsync(SqliteConnection connection, SqliteTransaction transaction, CheckoutCommit commit, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sales s LEFT JOIN outbox_messages o ON o.idempotency_key = $idempotency WHERE (s.tenant_id = $tenant AND s.store_id = $store AND s.terminal_id = $terminal AND s.local_transaction_id = $local) OR o.idempotency_key = $idempotency LIMIT 1;";
        AddScopeParameters(command, commit.Sale);
        command.Parameters.AddWithValue("$terminal", commit.Sale.TerminalId);
        command.Parameters.AddWithValue("$local", commit.Sale.LocalTransactionId);
        command.Parameters.AddWithValue("$idempotency", commit.OutboxMessage.IdempotencyKey);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task InsertSaleAsync(SqliteConnection connection, SqliteTransaction transaction, Sale sale, CancellationToken cancellationToken)
    {
        var command = CreateCommand(connection, transaction, "INSERT INTO sales VALUES ($sale, $tenant, $store, $terminal, $local, $subtotal, $tax, $discount, $total, $currency, $state, $occurred);");
        command.Parameters.AddWithValue("$sale", sale.SaleId);
        AddScopeParameters(command, sale);
        command.Parameters.AddWithValue("$terminal", sale.TerminalId);
        command.Parameters.AddWithValue("$local", sale.LocalTransactionId);
        command.Parameters.AddWithValue("$subtotal", sale.Subtotal.MinorUnits);
        command.Parameters.AddWithValue("$tax", sale.Tax.MinorUnits);
        command.Parameters.AddWithValue("$discount", sale.Discount.MinorUnits);
        command.Parameters.AddWithValue("$total", sale.Total.MinorUnits);
        command.Parameters.AddWithValue("$currency", sale.Total.Currency);
        command.Parameters.AddWithValue("$state", sale.State.ToString());
        command.Parameters.AddWithValue("$occurred", sale.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertSaleLinesAsync(SqliteConnection connection, SqliteTransaction transaction, Sale sale, CancellationToken cancellationToken)
    {
        for (var index = 0; index < sale.Lines.Count; index++)
        {
            var line = sale.Lines[index];
            var command = CreateCommand(connection, transaction, "INSERT INTO sale_lines VALUES ($tenant, $store, $sale, $number, $product, $name, $quantity, $unitPrice, $currency, $taxCategory);");
            AddScopeParameters(command, sale);
            command.Parameters.AddWithValue("$sale", sale.SaleId);
            command.Parameters.AddWithValue("$number", index);
            command.Parameters.AddWithValue("$product", line.ProductId);
            command.Parameters.AddWithValue("$name", line.ProductName);
            command.Parameters.AddWithValue("$quantity", line.Quantity);
            command.Parameters.AddWithValue("$unitPrice", line.UnitPrice.MinorUnits);
            command.Parameters.AddWithValue("$currency", line.UnitPrice.Currency);
            command.Parameters.AddWithValue("$taxCategory", line.TaxCategory);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertPaymentAsync(SqliteConnection connection, SqliteTransaction transaction, CheckoutCommit commit, CancellationToken cancellationToken)
    {
        var command = CreateCommand(connection, transaction, "INSERT INTO payment_captures VALUES ($tenant, $store, $sale, $status, $reference, $auth);");
        AddScopeParameters(command, commit.Sale);
        command.Parameters.AddWithValue("$sale", commit.Sale.SaleId);
        command.Parameters.AddWithValue("$status", commit.Payment.Status.ToString());
        command.Parameters.AddWithValue("$reference", commit.Payment.ProviderTransactionReference);
        command.Parameters.AddWithValue("$auth", (object?)commit.Payment.AuthorizationCode ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertInventoryAsync(SqliteConnection connection, SqliteTransaction transaction, CheckoutCommit commit, CancellationToken cancellationToken)
    {
        for (var index = 0; index < commit.InventoryMovements.Count; index++)
        {
            var movement = commit.InventoryMovements[index];
            var command = CreateCommand(connection, transaction, "INSERT INTO inventory_movements VALUES ($tenant, $store, $sale, $number, $product, $delta);");
            AddScopeParameters(command, commit.Sale);
            command.Parameters.AddWithValue("$sale", commit.Sale.SaleId);
            command.Parameters.AddWithValue("$number", index);
            command.Parameters.AddWithValue("$product", movement.ProductId);
            command.Parameters.AddWithValue("$delta", movement.QuantityDelta);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertReceiptAsync(SqliteConnection connection, SqliteTransaction transaction, CheckoutCommit commit, CancellationToken cancellationToken)
    {
        var command = CreateCommand(connection, transaction, "INSERT INTO receipt_intents VALUES ($tenant, $store, $sale);");
        AddScopeParameters(command, commit.Sale);
        command.Parameters.AddWithValue("$sale", commit.ReceiptIntent.SaleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxAsync(SqliteConnection connection, SqliteTransaction transaction, CheckoutCommit commit, CancellationToken cancellationToken)
    {
        var command = CreateCommand(connection, transaction, "INSERT INTO outbox_messages (message_id, tenant_id, store_id, idempotency_key, message_type, occurred_at, schema_version, payload_json) VALUES ($message, $tenant, $store, $idempotency, $type, $occurred, $version, $payload);");
        command.Parameters.AddWithValue("$message", commit.OutboxMessage.MessageId);
        command.Parameters.AddWithValue("$tenant", commit.OutboxMessage.TenantId);
        command.Parameters.AddWithValue("$store", commit.OutboxMessage.StoreId);
        command.Parameters.AddWithValue("$idempotency", commit.OutboxMessage.IdempotencyKey);
        command.Parameters.AddWithValue("$type", commit.OutboxMessage.MessageType);
        command.Parameters.AddWithValue("$occurred", commit.OutboxMessage.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$version", commit.OutboxMessage.SchemaVersion);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(commit.OutboxMessage.Payload));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void AddScopeParameters(SqliteCommand command, Sale sale)
    {
        command.Parameters.AddWithValue("$tenant", sale.TenantId);
        command.Parameters.AddWithValue("$store", sale.StoreId);
    }

    private static void ValidateScope(CheckoutCommit commit)
    {
        if (commit.Payment.TenantId != commit.Sale.TenantId || commit.ReceiptIntent.TenantId != commit.Sale.TenantId ||
            commit.OutboxMessage.TenantId != commit.Sale.TenantId || commit.OutboxMessage.StoreId != commit.Sale.StoreId ||
            commit.OutboxMessage.Payload.TenantId != commit.Sale.TenantId || commit.OutboxMessage.Payload.StoreId != commit.Sale.StoreId ||
            commit.ReceiptIntent.SaleId != commit.Sale.SaleId)
        {
            throw new CheckoutValidationException("Checkout records must share the same tenant, store, and sale scope.");
        }
    }
}