namespace RetailPulse.BuildingBlocks;

public sealed record PushSubscription(string TenantId, string StoreId, string SubjectId, string Endpoint, string P256dh, string Auth, DateTimeOffset UpdatedAt);

public interface IPushSubscriptionStore
{
    Task RegisterAsync(PushSubscription subscription, CancellationToken cancellationToken = default);
    Task RemoveAsync(TenantStoreScope scope, string subjectId, string endpoint, CancellationToken cancellationToken = default);
}
