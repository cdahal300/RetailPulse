namespace RetailPulse.BuildingBlocks;

public sealed record OperationalAlert(string AlertId, string TenantId, string StoreId, string Severity, string Category, string Title, string Detail, DateTimeOffset OccurredAt);
public sealed record NotificationPreferences(string TenantId, string StoreId, string SubjectId, bool LowStockEnabled, bool SyncFailureEnabled, DateTimeOffset UpdatedAt);
public sealed record NotificationPayload(string Title, string Body, string? Url = null, string? Tag = null);
public sealed record PushNotificationWorkItem(string EventType, string PayloadJson);

public interface IPushNotificationQueue
{
    ValueTask EnqueueAsync(PushNotificationWorkItem workItem, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PushNotificationWorkItem> ReadAllAsync(CancellationToken cancellationToken = default);
}

public interface IPushNotificationSender
{
    Task SendAsync(PushSubscription subscription, NotificationPayload payload, CancellationToken cancellationToken = default);
}

public sealed class NoOpPushNotificationSender : IPushNotificationSender
{
    public Task SendAsync(PushSubscription subscription, NotificationPayload payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public interface IAlertsReader
{
    Task<IReadOnlyList<OperationalAlert>> GetAlertsAsync(TenantStoreScope scope, CancellationToken cancellationToken = default);
    Task<NotificationPreferences> GetPreferencesAsync(TenantStoreScope scope, string subjectId, CancellationToken cancellationToken = default);
    Task<NotificationPreferences> SetPreferencesAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default);
}
