using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;
using Microsoft.Data.Sqlite;

namespace RetailPulse.IntegrationTests;

public class OutboxSyncIntegrationTests
{
    [Fact]
    public async Task Processor_marks_message_synced_and_health_is_scope_filtered()
    {
        await using var database = new TemporaryDatabase();
        var persistence = new SqliteCheckoutPersistence(database.Path);
        await persistence.CommitAsync(CreateCommit("tenant-1", "store-1", "message-1"));
        await persistence.CommitAsync(CreateCommit("tenant-2", "store-2", "message-2"));

        var processor = new OutboxSyncProcessor(persistence, new FixedClient(new(SyncSubmissionOutcome.Accepted, SyncAttemptClassification.Accepted)));
        Assert.Equal(1, await processor.ProcessAsync(new("tenant-1", "store-1"), now: DateTimeOffset.UnixEpoch));

        var health = await persistence.GetSyncHealthAsync(new("tenant-1", "store-1"));
        Assert.Equal(0, health.PendingCount);
        Assert.Equal(DateTimeOffset.UnixEpoch, health.LastSuccessAt);
        Assert.Equal(1, (await persistence.GetSyncHealthAsync(new("tenant-2", "store-2"))).PendingCount);
    }

    [Fact]
    public async Task Processor_keeps_transient_failure_retryable_and_dead_letters_invalid_payload()
    {
        await using var database = new TemporaryDatabase();
        var persistence = new SqliteCheckoutPersistence(database.Path);
        await persistence.CommitAsync(CreateCommit("tenant-1", "store-1", "message-1"));
        var now = DateTimeOffset.UnixEpoch;
        var retry = new OutboxSyncProcessor(persistence, new FixedClient(new(SyncSubmissionOutcome.Rejected, SyncAttemptClassification.TransientFailure, "offline")), new SyncRetryPolicy(3, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)));

        await retry.ProcessAsync(new("tenant-1", "store-1"), now: now);
        var retryHealth = await persistence.GetSyncHealthAsync(new("tenant-1", "store-1"));
        Assert.Equal(1, retryHealth.PendingCount);
        Assert.Equal(1, retryHealth.RetryCount);
        await using (var connection = new SqliteConnection($"Data Source={database.Path};Pooling=False"))
        {
            await connection.OpenAsync();
            var attempts = connection.CreateCommand();
            attempts.CommandText = "SELECT COUNT(*) FROM sync_attempts WHERE tenant_id = 'tenant-1' AND store_id = 'store-1';";
            Assert.Equal(1L, await attempts.ExecuteScalarAsync());
        }

        await persistence.MarkForReviewAsync("message-1", new("tenant-1", "store-1"), "payload conflict");
        var reviewHealth = await persistence.GetSyncHealthAsync(new("tenant-1", "store-1"));
        Assert.Equal(1, reviewHealth.ConflictCount);
        Assert.Equal(0, reviewHealth.PendingCount);
    }

    [Fact]
    public async Task Processor_dead_letters_unauthorized_delivery()
    {
        await using var database = new TemporaryDatabase();
        var persistence = new SqliteCheckoutPersistence(database.Path);
        await persistence.CommitAsync(CreateCommit("tenant-1", "store-1", "message-1"));
        var processor = new OutboxSyncProcessor(persistence, new FixedClient(new(SyncSubmissionOutcome.Rejected, SyncAttemptClassification.Unauthorized, "wrong device")));

        await processor.ProcessAsync(new("tenant-1", "store-1"), now: DateTimeOffset.UnixEpoch);

        var health = await persistence.GetSyncHealthAsync(new("tenant-1", "store-1"));
        Assert.Equal(1, health.DeadLetterCount);
        Assert.Equal(0, health.PendingCount);
    }

    private static CheckoutCommit CreateCommit(string tenant, string store, string messageId)
    {
        var sale = new Sale($"sale-{messageId}", tenant, store, "terminal-1", $"txn-{messageId}", [new SaleLine("sku-1", "Product", 1, new Money(100, "USD"), "standard")], new Money(100, "USD"), Money.Zero("USD"), Money.Zero("USD"), new Money(100, "USD"), SaleState.Completed, DateTimeOffset.UnixEpoch);
        var movement = new InventoryMovement("sku-1", -1);
        var payload = new SaleCompletedEvent($"event-{messageId}", sale.SaleId, tenant, store, sale.OccurredAt, 1, "correlation-1", "store-edge", sale.SaleId, sale.LocalTransactionId, "USD", 100, "opaque-ref", [movement]);
        var outbox = new OutboxMessage(messageId, "SaleCompleted", tenant, store, CheckoutIdempotency.Create(tenant, store, sale.TerminalId, sale.LocalTransactionId), sale.OccurredAt, 1, payload);
        return new(sale, new PaymentCapture(tenant, PaymentStatus.Approved, "opaque-ref", null), [movement], new ReceiptIntent(tenant, sale.SaleId), outbox);
    }

    private sealed class FixedClient(SyncSubmissionResult result) : ISyncSubmissionClient
    {
        public Task<SyncSubmissionResult> SubmitAsync(OutboxMessage message, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"retailpulse-sync-{Guid.NewGuid():N}.db");
        public ValueTask DisposeAsync()
        {
            if (File.Exists(Path)) File.Delete(Path);
            return ValueTask.CompletedTask;
        }
    }
}
