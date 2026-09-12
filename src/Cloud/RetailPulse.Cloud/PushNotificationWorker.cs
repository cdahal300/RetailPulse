using System.Text.Json;
using System.Threading.Channels;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PushNotificationQueue : IPushNotificationQueue
{
    private readonly Channel<PushNotificationWorkItem> channel = Channel.CreateBounded<PushNotificationWorkItem>(new BoundedChannelOptions(512)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(PushNotificationWorkItem workItem, CancellationToken cancellationToken = default) => channel.Writer.WriteAsync(workItem, cancellationToken);

    public IAsyncEnumerable<PushNotificationWorkItem> ReadAllAsync(CancellationToken cancellationToken = default) => channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class PushNotificationWorker(
    IPushNotificationQueue queue,
    IPushSubscriptionStore subscriptions,
    IAlertsReader alerts,
    IPushNotificationSender sender,
    ILogger<PushNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await DeliverAsync(workItem, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Push notification delivery failed for event type {EventType}.", workItem.EventType);
            }
        }
    }

    private async Task DeliverAsync(PushNotificationWorkItem workItem, CancellationToken cancellationToken)
    {
        if (!string.Equals(workItem.EventType, nameof(LowStockDetectedV1), StringComparison.Ordinal)) return;
        var eventPayload = JsonSerializer.Deserialize<LowStockEventPayload>(workItem.PayloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (eventPayload is null) return;
        var tenantId = eventPayload.TenantId;
        var storeId = eventPayload.StoreId;
        var productId = eventPayload.ProductId;
        var quantity = eventPayload.Quantity;
        var minimumQuantity = eventPayload.MinimumQuantity;
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId) || string.IsNullOrWhiteSpace(productId)) return;

        var scope = new TenantStoreScope(tenantId, storeId);
        foreach (var subscription in await subscriptions.ListAsync(scope, cancellationToken))
        {
            var preferences = await alerts.GetPreferencesAsync(scope, subscription.SubjectId, cancellationToken);
            if (!preferences.LowStockEnabled) continue;
            await sender.SendAsync(subscription, new NotificationPayload(
                "Low stock alert",
                $"{productId} has {quantity} available; threshold is {minimumQuantity}.",
                "/#alerts",
                $"low-stock:{productId}"), cancellationToken);
        }
    }

    private sealed record LowStockEventPayload(string TenantId, string StoreId, string ProductId, int Quantity, int MinimumQuantity);
}
