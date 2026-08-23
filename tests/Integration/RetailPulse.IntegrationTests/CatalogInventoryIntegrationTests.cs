using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

namespace RetailPulse.IntegrationTests;

public class CatalogInventoryIntegrationTests
{
    [Fact]
    public async Task Catalog_lookup_and_inventory_balance_are_filtered_by_tenant_and_store()
    {
        var catalog = new InMemoryCatalogRepository();
        catalog.Add(Product("tenant-1", "store-1", "sku-1", "111"));
        catalog.Add(Product("tenant-2", "store-1", "sku-2", "111"));
        var ledger = new InMemoryInventoryLedger();
        var authorization = new InMemoryAuthorization();
        authorization.AssignCashier("tenant-1", "cashier-1", "store-1");
        authorization.AssignCashier("tenant-2", "cashier-2", "store-1");
        var service = new CatalogInventoryService(catalog, ledger, authorization);

        var tenantOne = await service.LookupAsync(new("tenant-1", "store-1"), "111", DateTimeOffset.UtcNow, "cashier-1", CatalogInventoryRole.Cashier);
        var tenantTwo = await service.LookupAsync(new("tenant-2", "store-1"), "111", DateTimeOffset.UtcNow, "cashier-2", CatalogInventoryRole.Cashier);
        var otherStore = await service.LookupAsync(new("tenant-1", "store-2"), "111", DateTimeOffset.UtcNow, "cashier-1", CatalogInventoryRole.Cashier);

        Assert.Equal("sku-1", tenantOne.Product!.ProductId);
        Assert.Equal("sku-2", tenantTwo.Product!.ProductId);
        Assert.Equal(CatalogInventoryErrorCode.Forbidden, otherStore.ErrorCode);
        Assert.Equal("tenant-1", tenantOne.Balance!.TenantId);
    }

    [Fact]
    public async Task Movement_append_materializes_balance_and_duplicate_delivery_is_idempotent()
    {
        var catalog = new InMemoryCatalogRepository();
        catalog.Add(Product("tenant-1", "store-1", "sku-1", "111"));
        var ledger = new InMemoryInventoryLedger();
        var authorization = new InMemoryAuthorization();
        authorization.AssignManager("tenant-1", "manager-1", "store-1");
        var service = new CatalogInventoryService(catalog, ledger, authorization);
        var command = new InventoryMovementCommand("tenant-1", "store-1", "movement-1", "sku-1", 3, InventoryMovementReason.Receipt, DateTimeOffset.UtcNow, 0, "manager-1", CatalogInventoryRole.Manager, "command-1", "correlation-1");

        var first = await service.ReceiveAsync(command);
        var duplicate = await service.ReceiveAsync(command);

        Assert.Equal(InventoryAppendOutcome.Appended, first.Outcome);
        Assert.Equal(InventoryAppendOutcome.Duplicate, duplicate.Outcome);
        Assert.Equal(3, (await ledger.GetBalanceAsync(new("tenant-1", "store-1"), "sku-1")).Quantity);
        Assert.Single(first.Events);
        var movementEvent = Assert.IsType<InventoryMovementRecordedV1>(first.Events[0]);
        Assert.Equal("tenant-1", movementEvent.TenantId);
        Assert.Equal("store-1", movementEvent.StoreId);
        Assert.Equal("tenant-1", movementEvent.Movement.TenantId);
        Assert.Equal(1, movementEvent.SchemaVersion);
    }

    private static CatalogProduct Product(string tenantId, string storeId, string productId, string barcode) => new(tenantId, storeId, productId, barcode, "Product", new(1000, "USD"), "standard", 700, true, DateTimeOffset.UnixEpoch, 1);
}