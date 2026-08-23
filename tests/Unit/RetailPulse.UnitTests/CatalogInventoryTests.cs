using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

namespace RetailPulse.UnitTests;

public class CatalogInventoryTests
{
    [Fact]
    public async Task Lookup_returns_effective_product_and_balance_for_authorized_scope()
    {
        var (service, _, _) = Create();
        var result = await service.LookupAsync(new("tenant-1", "store-1"), "123", DateTimeOffset.Parse("2026-01-02"), "cashier-1", CatalogInventoryRole.Cashier);

        Assert.True(result.IsSuccess);
        Assert.Equal(1100, result.Product!.Price.MinorUnits);
        Assert.Equal("standard", result.Product.TaxCategory);
    }

    [Fact]
    public async Task Lookup_uses_latest_effective_version_and_rejects_inactive_or_missing_products()
    {
        var (service, catalog, _) = Create();
        catalog.Add(Product("123", "Old", 1000, DateTimeOffset.Parse("2025-01-01"), 1));
        catalog.Add(Product("123", "New", 1100, DateTimeOffset.Parse("2026-01-01"), 2));
        catalog.Add(Product("999", "Inactive", 1000, DateTimeOffset.Parse("2026-01-01"), 1) with { IsActive = false });

        var effective = await service.LookupAsync(new("tenant-1", "store-1"), "123", DateTimeOffset.Parse("2026-01-02"), "cashier-1", CatalogInventoryRole.Cashier);
        var inactive = await service.LookupAsync(new("tenant-1", "store-1"), "999", DateTimeOffset.UtcNow, "cashier-1", CatalogInventoryRole.Cashier);
        var missing = await service.LookupAsync(new("tenant-1", "store-1"), "missing", DateTimeOffset.UtcNow, "cashier-1", CatalogInventoryRole.Cashier);

        Assert.Equal("New", effective.Product!.Name);
        Assert.Equal(CatalogInventoryErrorCode.NotFound, inactive.ErrorCode);
        Assert.Equal(CatalogInventoryErrorCode.NotFound, missing.ErrorCode);
    }

    [Fact]
    public async Task Receipt_adjustment_duplicate_negative_stock_conflict_and_low_stock_are_explicit()
    {
        var (service, _, ledger) = Create();
        await service.ConfigureThresholdAsync(new("tenant-1", "store-1"), "owner-1", CatalogInventoryRole.Owner, "sku-1", 2, 0);
        var receipt = Command(5, 0, "receipt-1", InventoryMovementReason.Receipt);
        var received = await service.ReceiveAsync(receipt);
        var duplicate = await service.ReceiveAsync(receipt);
        var lowStock = await service.AdjustAsync(Command(-4, 1, "adjust-1", InventoryMovementReason.Adjustment));
        var negative = await service.AdjustAsync(Command(-2, 2, "adjust-2", InventoryMovementReason.Adjustment));
        var stale = await service.AdjustAsync(Command(1, 0, "adjust-3", InventoryMovementReason.Adjustment));

        Assert.Equal(InventoryAppendOutcome.Appended, received.Outcome);
        Assert.Equal(5, received.Balance.Quantity);
        Assert.Equal(InventoryAppendOutcome.Duplicate, duplicate.Outcome);
        Assert.Equal(InventoryAppendOutcome.Appended, lowStock.Outcome);
        Assert.Contains(lowStock.Events, @event => @event is LowStockDetectedV1);
        Assert.Equal(CatalogInventoryErrorCode.NegativeStock, negative.ErrorCode);
        Assert.Equal(CatalogInventoryErrorCode.StaleVersion, stale.ErrorCode);
        Assert.Contains(stale.Events, @event => @event is InventoryConflictDetectedV1);
        Assert.Equal(1, (await ledger.GetBalanceAsync(new("tenant-1", "store-1"), "sku-1")).Quantity);
    }

    [Fact]
    public async Task Cashier_cannot_adjust_manager_scope_and_owner_can_configure_threshold()
    {
        var (service, _, _) = Create();
        var forbidden = await service.AdjustAsync(Command(1, 0, "adjust-1", InventoryMovementReason.Adjustment) with { Role = CatalogInventoryRole.Cashier });
        var threshold = await service.ConfigureThresholdAsync(new("tenant-1", "store-1"), "owner-1", CatalogInventoryRole.Owner, "sku-1", 2, 0);

        Assert.Equal(CatalogInventoryErrorCode.Forbidden, forbidden.ErrorCode);
        Assert.True(threshold.IsSuccess);
        Assert.Equal(2, threshold.Threshold!.MinimumQuantity);
    }

    private static (CatalogInventoryService Service, InMemoryCatalogRepository Catalog, InMemoryInventoryLedger Ledger) Create()
    {
        var catalog = new InMemoryCatalogRepository();
        catalog.Add(Product("123", "Coffee", 1100, DateTimeOffset.Parse("2026-01-01"), 1));
        catalog.Add(Product("sku-1", "Stock item", 500, DateTimeOffset.Parse("2026-01-01"), 1));
        var ledger = new InMemoryInventoryLedger();
        var authorization = new InMemoryAuthorization();
        authorization.AssignCashier("tenant-1", "cashier-1", "store-1");
        authorization.AssignManager("tenant-1", "manager-1", "store-1");
        authorization.AssignOwner("tenant-1", "owner-1");
        return (new(catalog, ledger, authorization), catalog, ledger);
    }

    private static CatalogProduct Product(string barcode, string name, long price, DateTimeOffset effectiveAt, int version) => new("tenant-1", "store-1", barcode == "123" ? "sku-1" : barcode, barcode, name, new(price, "USD"), "standard", 700, true, effectiveAt, version);
    private static InventoryMovementCommand Command(int delta, int version, string id, InventoryMovementReason reason) => new("tenant-1", "store-1", id, "sku-1", delta, reason, DateTimeOffset.UtcNow, version, "manager-1", CatalogInventoryRole.Manager, id, "correlation-1");
}