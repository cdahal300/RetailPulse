using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class CloudCatalogRepository : ICatalogRepository
{
    private readonly Dictionary<(string TenantId, string StoreId, string ProductId), CatalogProduct> products = new()
    {
        [("tenant-1", "store-1", "coffee")] = new("tenant-1", "store-1", "coffee", "coffee", "Coffee", new(1000, "USD"), "standard", 700, true, DateTimeOffset.UnixEpoch, 1),
        [("tenant-1", "store-1", "tea")] = new("tenant-1", "store-1", "tea", "tea", "Tea", new(1200, "USD"), "standard", 700, true, DateTimeOffset.UnixEpoch, 1),
        [("tenant-1", "store-1", "sandwich")] = new("tenant-1", "store-1", "sandwich", "sandwich", "Sandwich", new(2550, "USD"), "standard", 700, true, DateTimeOffset.UnixEpoch, 1)
    };

    public Task<CatalogProduct?> FindByBarcodeAsync(CatalogQuery query, string barcode, CancellationToken cancellationToken = default) =>
        Task.FromResult(products.Values.FirstOrDefault(product => product.TenantId == query.Scope.TenantId && product.StoreId == query.Scope.StoreId && product.Barcode == barcode));

    public Task<CatalogProduct?> FindByProductIdAsync(CatalogQuery query, string productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(products.GetValueOrDefault((query.Scope.TenantId, query.Scope.StoreId, productId)));
}

public sealed class CloudInventoryAuthorization : ICatalogInventoryAuthorization
{
    public bool IsAuthorized(CatalogInventoryScope scope, string actorId, CatalogInventoryRole role, CatalogInventoryOperation operation) =>
        scope.IsValid && !string.IsNullOrWhiteSpace(actorId) && role == CatalogInventoryRole.Manager && operation == CatalogInventoryOperation.Adjust;
}

public sealed class CloudInventoryLedger : IInventoryLedgerRepository
{
    private readonly Dictionary<(string TenantId, string StoreId, string ProductId), InventoryBalance> balances = [];
    private readonly List<InventoryMovementRecord> movements = [];
    private readonly Dictionary<(string TenantId, string StoreId, string ProductId), InventoryThreshold> thresholds = [];

    public Task<InventoryAppendResult> AppendAsync(InventoryMovementRecord movement, CancellationToken cancellationToken = default)
    {
        var key = (movement.TenantId, movement.StoreId, movement.ProductId);
        if (movements.Any(existing => existing.TenantId == movement.TenantId && existing.StoreId == movement.StoreId && (existing.MovementId == movement.MovementId || existing.CommandId == movement.CommandId)))
        {
            return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.Duplicate, balances.GetValueOrDefault(key, new(movement.TenantId, movement.StoreId, movement.ProductId, 0, 0)), movement));
        }

        var current = balances.GetValueOrDefault(key, new(movement.TenantId, movement.StoreId, movement.ProductId, 0, 0));
        if (current.Version != movement.ExpectedVersion)
        {
            return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.StaleVersion, current, Error: $"Expected version {movement.ExpectedVersion}, actual version {current.Version}."));
        }

        var quantity = current.Quantity + movement.QuantityDelta;
        if (quantity < 0)
        {
            return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.NegativeStock, current, Error: "Movement would create negative stock."));
        }

        var balance = new InventoryBalance(movement.TenantId, movement.StoreId, movement.ProductId, quantity, current.Version + 1);
        balances[key] = balance;
        movements.Add(movement with { AggregateVersion = balance.Version });
        return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.Appended, balance, movements[^1]));
    }

    public Task<InventoryBalance> GetBalanceAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(balances.GetValueOrDefault((scope.TenantId, scope.StoreId, productId), new(scope.TenantId, scope.StoreId, productId, 0, 0)));

    public Task<InventoryMovementRecord?> FindMovementAsync(CatalogInventoryScope scope, string movementId, string commandId, CancellationToken cancellationToken = default) =>
        Task.FromResult(movements.FirstOrDefault(movement => movement.TenantId == scope.TenantId && movement.StoreId == scope.StoreId && (movement.MovementId == movementId || movement.CommandId == commandId)));

    public Task<InventoryThreshold?> GetThresholdAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(thresholds.GetValueOrDefault((scope.TenantId, scope.StoreId, productId)));

    public Task SetThresholdAsync(InventoryThreshold threshold, CancellationToken cancellationToken = default)
    {
        thresholds[(threshold.TenantId, threshold.StoreId, threshold.ProductId)] = threshold;
        return Task.CompletedTask;
    }
}
