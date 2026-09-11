namespace RetailPulse.BuildingBlocks;

public sealed record OperationalAlert(string AlertId, string TenantId, string StoreId, string Severity, string Category, string Title, string Detail, DateTimeOffset OccurredAt);
public sealed record NotificationPreferences(string TenantId, string StoreId, string SubjectId, bool LowStockEnabled, bool SyncFailureEnabled, DateTimeOffset UpdatedAt);

public interface IAlertsReader
{
    Task<IReadOnlyList<OperationalAlert>> GetAlertsAsync(TenantStoreScope scope, CancellationToken cancellationToken = default);
    Task<NotificationPreferences> GetPreferencesAsync(TenantStoreScope scope, string subjectId, CancellationToken cancellationToken = default);
    Task<NotificationPreferences> SetPreferencesAsync(NotificationPreferences preferences, CancellationToken cancellationToken = default);
}
