namespace RetailPulse.BuildingBlocks;

public sealed record StoreSettings(string TenantId, string StoreId, string DisplayName, string TimeZone, string Currency, bool InventoryAdjustmentsEnabled, int Version);

public interface IStoreSettingsRepository
{
    Task<StoreSettings> GetAsync(TenantStoreScope scope, CancellationToken cancellationToken = default);
    Task<StoreSettings?> UpdateAsync(StoreSettings settings, int expectedVersion, CancellationToken cancellationToken = default);
}