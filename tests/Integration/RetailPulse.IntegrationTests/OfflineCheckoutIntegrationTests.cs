using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;
using Microsoft.Data.Sqlite;

namespace RetailPulse.IntegrationTests;

public class OfflineCheckoutIntegrationTests
{
    [Fact]
    public async Task SQLite_commit_is_atomic_and_survives_reopen()
    {
        await using var database = TemporaryDatabase.Create();
        var persistence = new SqliteCheckoutPersistence(database.Path);
        var commit = CreateCommit();

        await persistence.CommitAsync(commit);
        var health = await persistence.GetHealthAsync();
        Assert.True(health.Available);
        Assert.Equal(SqliteCheckoutPersistence.CurrentSchemaVersion, health.SchemaVersion);
        Assert.Equal(1, health.PendingOutboxCount);
        await AssertTableCountAsync(database.Path, "sales", 1);
        await AssertTableCountAsync(database.Path, "sale_lines", 1);
        await AssertTableCountAsync(database.Path, "payment_captures", 1);
        await AssertTableCountAsync(database.Path, "inventory_movements", 1);
        await AssertTableCountAsync(database.Path, "receipt_intents", 1);
        await AssertTableCountAsync(database.Path, "outbox_messages", 1);
        await AssertTableCountAsync(database.Path, "schema_migrations", 1);

        await using var reopened = new SqliteCheckoutPersistence(database.Path);
        Assert.Equal(1, (await reopened.GetHealthAsync()).PendingOutboxCount);
        await reopened.CommitAsync(commit);
        Assert.Equal(1, (await reopened.GetHealthAsync()).PendingOutboxCount);
    }

    [Fact]
    public async Task SQLite_failure_rolls_back_sale_and_dependent_records()
    {
        await using var database = TemporaryDatabase.Create();
        var persistence = new SqliteCheckoutPersistence(database.Path);
        var commit = CreateCommit() with { InventoryMovements = [new InventoryMovement("", -1)] };

        await Assert.ThrowsAsync<SqliteException>(() => persistence.CommitAsync(commit));
        await using var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await connection.OpenAsync();
        foreach (var table in new[] { "sales", "sale_lines", "payment_captures", "inventory_movements", "receipt_intents", "outbox_messages" })
        {
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            Assert.Equal(0L, await command.ExecuteScalarAsync());
        }
    }

    [Fact]
    public async Task SQLite_rejects_wrong_tenant_or_store_scope_and_has_no_card_columns()
    {
        await using var database = TemporaryDatabase.Create();
        var persistence = new SqliteCheckoutPersistence(database.Path);
        var commit = CreateCommit() with { OutboxMessage = CreateCommit().OutboxMessage with { StoreId = "other-store" } };

        await Assert.ThrowsAsync<CheckoutValidationException>(() => persistence.CommitAsync(commit));
        await using var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        var schemas = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) schemas.Add(reader.GetString(0));
        Assert.DoesNotContain(schemas, schema => schema.Contains("pan", StringComparison.OrdinalIgnoreCase) || schema.Contains("cvv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Edge_commit_is_local_and_contains_sale_payment_inventory_and_outbox()
    {
        var payment = new FakePaymentProvider([PaymentResult.Approved("edge-provider-ref")]);
        var persistence = new InMemoryCheckoutPersistence();
        var cart = new Cart("cart-1", "USD");
        cart.Add(new Product("sku-1", "Product", new Money(2500, "USD"), "standard", 800), 1);

        var result = await new CheckoutService(payment, persistence).CheckoutAsync(new CheckoutRequest(cart, "tenant-1", "store-1", "terminal-1", "txn-1", "correlation-1"));

        Assert.True(result.IsCompleted);
        var commit = Assert.Single(persistence.Commits);
        Assert.Equal("edge-provider-ref", commit.Payment.ProviderTransactionReference);
        Assert.Equal(commit.Sale.SaleId, commit.ReceiptIntent.SaleId);
        Assert.Single(commit.InventoryMovements);
        Assert.Equal(-1, commit.InventoryMovements[0].QuantityDelta);
        Assert.Equal("tenant-1", commit.Sale.TenantId);
        Assert.Equal("tenant-1", payment.Requests[0].TenantId);
        Assert.Equal("v1|tenantId=8:tenant-1|storeId=7:store-1|terminalId=10:terminal-1|localTransactionId=5:txn-1", commit.OutboxMessage.IdempotencyKey);
        Assert.Equal("tenant-1", commit.OutboxMessage.Payload.TenantId);
        Assert.Single(payment.Requests);
    }

    [Fact]
    public async Task Pending_payment_does_not_create_local_sale_or_outbox()
    {
        var persistence = new InMemoryCheckoutPersistence();
        var cart = new Cart("cart-1", "USD");
        cart.Add(new Product("sku-1", "Product", new Money(500, "USD"), "standard", 0), 1);

        var result = await new CheckoutService(new FakePaymentProvider([PaymentResult.Pending()]), persistence)
            .CheckoutAsync(new CheckoutRequest(cart, "tenant-1", "store-1", "terminal-1", "txn-2", "correlation-2"));

        Assert.Equal(CheckoutOutcome.Pending, result.Outcome);
        Assert.Empty(persistence.Commits);
    }

    private static CheckoutCommit CreateCommit()
    {
        var cart = new Cart("cart-1", "USD");
        cart.Add(new Product("sku-1", "Product", new Money(2500, "USD"), "standard", 800), 1);
        var sale = new Sale("sale-1", "tenant-1", "store-1", "terminal-1", "txn-1", [new SaleLine("sku-1", "Product", 1, new Money(2500, "USD"), "standard")], new Money(2500, "USD"), new Money(200, "USD"), Money.Zero("USD"), new Money(2700, "USD"), SaleState.Completed, DateTimeOffset.UnixEpoch);
        var movement = new InventoryMovement("sku-1", -1);
        var payload = new SaleCompletedEvent("event-1", sale.SaleId, sale.TenantId, sale.StoreId, sale.OccurredAt, 1, "correlation-1", "store-edge", sale.SaleId, sale.LocalTransactionId, "USD", sale.Total.MinorUnits, "provider-1", [movement]);
        var outbox = new OutboxMessage("message-1", "SaleCompleted", sale.TenantId, sale.StoreId, CheckoutIdempotency.Create(sale.TenantId, sale.StoreId, sale.TerminalId, sale.LocalTransactionId), sale.OccurredAt, 1, payload);
        return new(sale, new PaymentCapture(sale.TenantId, PaymentStatus.Approved, "provider-1", "auth-1"), [movement], new ReceiptIntent(sale.TenantId, sale.SaleId), outbox);
    }

    private static async Task AssertTableCountAsync(string databasePath, string table, long expected)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        Assert.Equal(expected, await command.ExecuteScalarAsync());
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"retailpulse-{Guid.NewGuid():N}.db");
        public static TemporaryDatabase Create() => new();
        public ValueTask DisposeAsync()
        {
            if (File.Exists(Path)) File.Delete(Path);
            return ValueTask.CompletedTask;
        }
    }
}