using RetailPulse.BuildingBlocks;
using RetailPulse.Cloud;
using RetailPulse.Edge;

namespace RetailPulse.IntegrationTests;

public class CloudSyncIntegrationTests
{
    [Fact]
    public async Task Cloud_accepts_once_and_returns_duplicate_without_duplicate_effect()
    {
        var authorization = new InMemorySyncAuthorization();
        var scope = new TenantStoreScope("tenant-1", "store-1");
        authorization.Register(scope, "terminal-1");
        var handler = new InMemoryCloudSyncHandler(authorization);
        var message = CreateMessage();

        var first = await handler.SubmitAsync(message, scope, "terminal-1");
        var duplicate = await handler.SubmitAsync(message, scope, "terminal-1");

        Assert.Equal(SyncSubmissionOutcome.Accepted, first.Outcome);
        Assert.Equal(SyncSubmissionOutcome.Duplicate, duplicate.Outcome);
        Assert.Equal(1, handler.AppliedSaleCount);
    }

    [Fact]
    public async Task Cloud_rejects_wrong_scope_without_mutation()
    {
        var authorization = new InMemorySyncAuthorization();
        var source = new TenantStoreScope("tenant-1", "store-1");
        var wrong = new TenantStoreScope("tenant-2", "store-2");
        authorization.Register(source, "terminal-1");
        var handler = new InMemoryCloudSyncHandler(authorization);

        var result = await handler.SubmitAsync(CreateMessage(), wrong, "terminal-1");

        Assert.Equal(SyncSubmissionOutcome.Rejected, result.Outcome);
        Assert.Equal(SyncAttemptClassification.Unauthorized, result.Classification);
        Assert.Equal(0, handler.AppliedSaleCount);
    }

    private static OutboxMessage CreateMessage()
    {
        var movement = new InventoryMovement("sku-1", -1);
        var payload = new SaleCompletedEvent("event-1", "sale-1", "tenant-1", "store-1", DateTimeOffset.UnixEpoch, 1, "correlation-1", "store-edge", "sale-1", "txn-1", "USD", 100, "opaque-payment-ref", [movement]);
        return new("message-1", "SaleCompleted", "tenant-1", "store-1", "idempotency-1", DateTimeOffset.UnixEpoch, 1, payload);
    }
}
