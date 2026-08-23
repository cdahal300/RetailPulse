using RetailPulse.BuildingBlocks;

namespace RetailPulse.Edge;

public sealed class InMemoryAuthorization : ICatalogInventoryAuthorization
{
    private readonly Dictionary<(string TenantId, string ActorId), HashSet<string>> cashierStores = [];
    private readonly Dictionary<(string TenantId, string ActorId), HashSet<string>> managerStores = [];
        public void AssignCashier(string tenantId, string actorId, string storeId)
        {
            if (!cashierStores.TryGetValue((tenantId, actorId), out var stores))
            {
                stores = [];
                cashierStores[(tenantId, actorId)] = stores;
            }
            stores.Add(storeId);
        }

    private readonly HashSet<(string TenantId, string ActorId)> owners = [];

    public void AssignManager(string tenantId, string actorId, string storeId)
    {
        if (!managerStores.TryGetValue((tenantId, actorId), out var stores))
        {
            stores = [];
            managerStores[(tenantId, actorId)] = stores;
        }
        stores.Add(storeId);
    }

    public void AssignOwner(string tenantId, string actorId) => owners.Add((tenantId, actorId));

    public bool IsAuthorized(CatalogInventoryScope scope, string actorId, CatalogInventoryRole role, CatalogInventoryOperation operation) =>
        operation == CatalogInventoryOperation.Lookup && role == CatalogInventoryRole.Cashier && cashierStores.TryGetValue((scope.TenantId, actorId), out var cashierScope) && cashierScope.Contains(scope.StoreId) ||
        operation == CatalogInventoryOperation.Lookup && role == CatalogInventoryRole.Manager && managerStores.TryGetValue((scope.TenantId, actorId), out var managerScope) && managerScope.Contains(scope.StoreId) ||
        operation == CatalogInventoryOperation.Lookup && role == CatalogInventoryRole.Owner && owners.Contains((scope.TenantId, actorId)) ||
        operation is CatalogInventoryOperation.Receive or CatalogInventoryOperation.Adjust && role == CatalogInventoryRole.Manager && managerStores.TryGetValue((scope.TenantId, actorId), out var stores) && stores.Contains(scope.StoreId) ||
        operation == CatalogInventoryOperation.ConfigureThreshold && role == CatalogInventoryRole.Owner && owners.Contains((scope.TenantId, actorId));
}

public sealed class InMemoryCatalogRepository : ICatalogRepository
{
    private readonly List<CatalogProduct> products = [];

    public void Add(CatalogProduct product) => products.Add(product);

    public Task<CatalogProduct?> FindByBarcodeAsync(CatalogQuery query, string barcode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Find(query, product => product.Barcode == barcode));

    public Task<CatalogProduct?> FindByProductIdAsync(CatalogQuery query, string productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Find(query, product => product.ProductId == productId));

    private CatalogProduct? Find(CatalogQuery query, Func<CatalogProduct, bool> predicate) => products
        .Where(product => product.TenantId == query.Scope.TenantId && product.StoreId == query.Scope.StoreId && product.EffectiveAt <= query.EffectiveAt && (query.Version is null || product.Version <= query.Version) && predicate(product))
        .OrderByDescending(product => product.EffectiveAt)
        .ThenByDescending(product => product.Version)
        .FirstOrDefault();
}

public sealed class InMemoryInventoryLedger : IInventoryLedgerRepository
{
    private readonly Dictionary<(string TenantId, string StoreId, string ProductId), InventoryBalance> balances = [];
    private readonly List<InventoryMovementRecord> movements = [];
    private readonly Dictionary<(string TenantId, string StoreId, string ProductId), InventoryThreshold> thresholds = [];

    public Task<InventoryAppendResult> AppendAsync(InventoryMovementRecord movement, CancellationToken cancellationToken = default)
    {
        var key = (movement.TenantId, movement.StoreId, movement.ProductId);
        if (movements.Any(existing => existing.TenantId == movement.TenantId && existing.StoreId == movement.StoreId && (existing.MovementId == movement.MovementId || existing.CommandId == movement.CommandId)))
            return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.Duplicate, balances.GetValueOrDefault(key, new(movement.TenantId, movement.StoreId, movement.ProductId, 0, 0)), movement));
        var current = balances.GetValueOrDefault(key, new(movement.TenantId, movement.StoreId, movement.ProductId, 0, 0));
        if (current.Version != movement.ExpectedVersion)
            return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.StaleVersion, current, Error: $"Expected version {movement.ExpectedVersion}, actual version {current.Version}."));
        var quantity = current.Quantity + movement.QuantityDelta;
        if (quantity < 0)
            return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.NegativeStock, current, Error: "Movement would create negative stock."));
        var balance = new InventoryBalance(movement.TenantId, movement.StoreId, movement.ProductId, quantity, current.Version + 1);
        balances[key] = balance;
        movements.Add(movement with { AggregateVersion = balance.Version });
        return Task.FromResult(new InventoryAppendResult(InventoryAppendOutcome.Appended, balance, movements[^1]));
    }

    public Task<InventoryBalance> GetBalanceAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default) => Task.FromResult(balances.GetValueOrDefault((scope.TenantId, scope.StoreId, productId), new(scope.TenantId, scope.StoreId, productId, 0, 0)));
    public Task<InventoryMovementRecord?> FindMovementAsync(CatalogInventoryScope scope, string movementId, string commandId, CancellationToken cancellationToken = default) => Task.FromResult(movements.FirstOrDefault(movement => movement.TenantId == scope.TenantId && movement.StoreId == scope.StoreId && (movement.MovementId == movementId || movement.CommandId == commandId)));
    public Task<InventoryThreshold?> GetThresholdAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default) => Task.FromResult(thresholds.GetValueOrDefault((scope.TenantId, scope.StoreId, productId)));
    public Task SetThresholdAsync(InventoryThreshold threshold, CancellationToken cancellationToken = default) { thresholds[(threshold.TenantId, threshold.StoreId, threshold.ProductId)] = threshold; return Task.CompletedTask; }
}