namespace RetailPulse.BuildingBlocks;

public enum CatalogInventoryErrorCode { InvalidInput, Forbidden, NotFound, StaleVersion, NegativeStock, DuplicateMovement }
public enum CatalogInventoryRole { Cashier, Manager, Owner }
public enum InventoryMovementReason { Receipt, Adjustment, Sale, Return, Correction }

public readonly record struct CatalogInventoryScope(string TenantId, string StoreId)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(StoreId);
}

public sealed record CatalogProduct(string TenantId, string StoreId, string ProductId, string Barcode, string Name, Money Price, string TaxCategory, int TaxRateBasisPoints, bool IsActive, DateTimeOffset EffectiveAt, int Version);
public sealed record CatalogQuery(CatalogInventoryScope Scope, DateTimeOffset EffectiveAt, int? Version = null);

public interface ICatalogRepository
{
    Task<CatalogProduct?> FindByBarcodeAsync(CatalogQuery query, string barcode, CancellationToken cancellationToken = default);
    Task<CatalogProduct?> FindByProductIdAsync(CatalogQuery query, string productId, CancellationToken cancellationToken = default);
}

public sealed record InventoryMovementCommand(string TenantId, string StoreId, string MovementId, string ProductId, int QuantityDelta, InventoryMovementReason Reason, DateTimeOffset EffectiveAt, int ExpectedVersion, string ActorId, CatalogInventoryRole Role, string CommandId, string CorrelationId);
public sealed record InventoryMovementRecord(string TenantId, string StoreId, string MovementId, string ProductId, int QuantityDelta, InventoryMovementReason Reason, DateTimeOffset EffectiveAt, int ExpectedVersion, int AggregateVersion, string ActorId, CatalogInventoryRole Role, string CommandId, string CorrelationId);
public sealed record InventoryBalance(string TenantId, string StoreId, string ProductId, int Quantity, int Version);
public sealed record InventoryThreshold(string TenantId, string StoreId, string ProductId, int MinimumQuantity, int Version);

public interface IInventoryLedgerRepository
{
    Task<InventoryAppendResult> AppendAsync(InventoryMovementRecord movement, CancellationToken cancellationToken = default);
    Task<InventoryBalance> GetBalanceAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default);
    Task<InventoryMovementRecord?> FindMovementAsync(CatalogInventoryScope scope, string movementId, string commandId, CancellationToken cancellationToken = default);
    Task<InventoryThreshold?> GetThresholdAsync(CatalogInventoryScope scope, string productId, CancellationToken cancellationToken = default);
    Task SetThresholdAsync(InventoryThreshold threshold, CancellationToken cancellationToken = default);
}

public enum InventoryAppendOutcome { Appended, Duplicate, StaleVersion, NegativeStock }
public sealed record InventoryAppendResult(InventoryAppendOutcome Outcome, InventoryBalance Balance, InventoryMovementRecord? Movement = null, string? Error = null)
{
    public bool IsSuccess => Outcome is InventoryAppendOutcome.Appended or InventoryAppendOutcome.Duplicate;
}

public enum CatalogInventoryOperation { Lookup, Receive, Adjust, ConfigureThreshold }
public interface ICatalogInventoryAuthorization
{
    bool IsAuthorized(CatalogInventoryScope scope, string actorId, CatalogInventoryRole role, CatalogInventoryOperation operation);
}

public abstract record CatalogInventoryEvent(string EventId, string AggregateId, string TenantId, string StoreId, DateTimeOffset OccurredAt, string CorrelationId, int SchemaVersion);
public sealed record CatalogProductChangedV1(string EventId, string AggregateId, string TenantId, string StoreId, DateTimeOffset OccurredAt, string CorrelationId, CatalogProduct Product) : CatalogInventoryEvent(EventId, AggregateId, TenantId, StoreId, OccurredAt, CorrelationId, 1);
public sealed record InventoryMovementRecordedV1(string EventId, string AggregateId, string TenantId, string StoreId, DateTimeOffset OccurredAt, string CorrelationId, InventoryMovementRecord Movement, InventoryBalance Balance) : CatalogInventoryEvent(EventId, AggregateId, TenantId, StoreId, OccurredAt, CorrelationId, 1);
public sealed record InventoryConflictDetectedV1(string EventId, string AggregateId, string TenantId, string StoreId, DateTimeOffset OccurredAt, string CorrelationId, string ProductId, string MovementId, int ExpectedVersion, int ActualVersion, string Reason) : CatalogInventoryEvent(EventId, AggregateId, TenantId, StoreId, OccurredAt, CorrelationId, 1);
public sealed record LowStockDetectedV1(string EventId, string AggregateId, string TenantId, string StoreId, DateTimeOffset OccurredAt, string CorrelationId, string ProductId, int Quantity, int MinimumQuantity) : CatalogInventoryEvent(EventId, AggregateId, TenantId, StoreId, OccurredAt, CorrelationId, 1);

public sealed record CatalogLookupResult(CatalogProduct? Product, InventoryBalance? Balance, CatalogInventoryErrorCode? ErrorCode = null, string? Error = null)
{
    public bool IsSuccess => ErrorCode is null;
}
public sealed record InventoryCommandResult(InventoryAppendOutcome Outcome, InventoryBalance Balance, IReadOnlyList<CatalogInventoryEvent> Events, CatalogInventoryErrorCode? ErrorCode = null, string? Error = null)
{
    public bool IsSuccess => Outcome is InventoryAppendOutcome.Appended or InventoryAppendOutcome.Duplicate;
}
public sealed record ThresholdResult(InventoryThreshold? Threshold, CatalogInventoryErrorCode? ErrorCode = null, string? Error = null)
{
    public bool IsSuccess => ErrorCode is null;
}

public sealed class CatalogInventoryService(ICatalogRepository catalog, IInventoryLedgerRepository ledger, ICatalogInventoryAuthorization authorization)
{
    public async Task<CatalogLookupResult> LookupAsync(CatalogInventoryScope scope, string barcode, DateTimeOffset effectiveAt, string actorId, CatalogInventoryRole role, CancellationToken cancellationToken = default)
    {
        if (!scope.IsValid || string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(actorId)) return new(null, null, CatalogInventoryErrorCode.InvalidInput, "Tenant, store, barcode, and actor are required.");
        if (!authorization.IsAuthorized(scope, actorId, role, CatalogInventoryOperation.Lookup)) return new(null, null, CatalogInventoryErrorCode.Forbidden, "The actor cannot read this scope.");
        var product = await catalog.FindByBarcodeAsync(new(scope, effectiveAt), barcode, cancellationToken);
        if (product is null || !product.IsActive) return new(null, null, CatalogInventoryErrorCode.NotFound, "Active product was not found.");
        return new(product, await ledger.GetBalanceAsync(scope, product.ProductId, cancellationToken));
    }

    public Task<InventoryCommandResult> ReceiveAsync(InventoryMovementCommand command, CancellationToken cancellationToken = default) => ApplyMovementAsync(command with { Reason = InventoryMovementReason.Receipt }, cancellationToken);
    public Task<InventoryCommandResult> AdjustAsync(InventoryMovementCommand command, CancellationToken cancellationToken = default) => ApplyMovementAsync(command with { Reason = InventoryMovementReason.Adjustment }, cancellationToken);

    public async Task<ThresholdResult> ConfigureThresholdAsync(CatalogInventoryScope scope, string actorId, CatalogInventoryRole role, string productId, int minimumQuantity, int version, CancellationToken cancellationToken = default)
    {
        if (!scope.IsValid || string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(productId) || minimumQuantity < 0) return new(null, CatalogInventoryErrorCode.InvalidInput, "Threshold and scope values are invalid.");
        if (!authorization.IsAuthorized(scope, actorId, role, CatalogInventoryOperation.ConfigureThreshold)) return new(null, CatalogInventoryErrorCode.Forbidden, "The actor cannot configure thresholds for this scope.");
        var existing = await ledger.GetThresholdAsync(scope, productId, cancellationToken);
        if (existing is not null && existing.Version != version) return new(null, CatalogInventoryErrorCode.StaleVersion, "The threshold version is stale.");
        var threshold = new InventoryThreshold(scope.TenantId, scope.StoreId, productId, minimumQuantity, version + 1);
        await ledger.SetThresholdAsync(threshold, cancellationToken);
        return new(threshold);
    }

    private async Task<InventoryCommandResult> ApplyMovementAsync(InventoryMovementCommand command, CancellationToken cancellationToken)
    {
        var scope = new CatalogInventoryScope(command.TenantId, command.StoreId);
        if (!scope.IsValid || string.IsNullOrWhiteSpace(command.MovementId) || string.IsNullOrWhiteSpace(command.ProductId) || string.IsNullOrWhiteSpace(command.ActorId) || string.IsNullOrWhiteSpace(command.CommandId) || command.QuantityDelta == 0) return Failure(scope, command.ProductId, InventoryAppendOutcome.StaleVersion, CatalogInventoryErrorCode.InvalidInput, "Movement values are invalid.");
        var operation = command.Reason == InventoryMovementReason.Receipt ? CatalogInventoryOperation.Receive : CatalogInventoryOperation.Adjust;
        if (!authorization.IsAuthorized(scope, command.ActorId, command.Role, operation)) return Failure(scope, command.ProductId, InventoryAppendOutcome.StaleVersion, CatalogInventoryErrorCode.Forbidden, "The actor cannot change inventory for this scope.");
        var product = await catalog.FindByProductIdAsync(new(scope, command.EffectiveAt), command.ProductId, cancellationToken);
        if (product is null || !product.IsActive) return Failure(scope, command.ProductId, InventoryAppendOutcome.StaleVersion, CatalogInventoryErrorCode.NotFound, "Active product was not found.");
        var movement = new InventoryMovementRecord(command.TenantId, command.StoreId, command.MovementId, command.ProductId, command.QuantityDelta, command.Reason, command.EffectiveAt, command.ExpectedVersion, command.ExpectedVersion + 1, command.ActorId, command.Role, command.CommandId, command.CorrelationId);
        var append = await ledger.AppendAsync(movement, cancellationToken);
        if (!append.IsSuccess)
        {
            var code = append.Outcome == InventoryAppendOutcome.NegativeStock ? CatalogInventoryErrorCode.NegativeStock : CatalogInventoryErrorCode.StaleVersion;
            return Failure(scope, command.ProductId, append.Outcome, code, append.Error ?? "Inventory movement was rejected.", append.Balance, command);
        }
        var events = new List<CatalogInventoryEvent>();
        if (append.Outcome == InventoryAppendOutcome.Appended)
        {
            events.Add(new InventoryMovementRecordedV1(Guid.NewGuid().ToString("N"), command.ProductId, command.TenantId, command.StoreId, command.EffectiveAt, command.CorrelationId, append.Movement!, append.Balance));
            var threshold = await ledger.GetThresholdAsync(scope, command.ProductId, cancellationToken);
            if (threshold is not null && append.Balance.Quantity <= threshold.MinimumQuantity) events.Add(new LowStockDetectedV1(Guid.NewGuid().ToString("N"), command.ProductId, command.TenantId, command.StoreId, command.EffectiveAt, command.CorrelationId, command.ProductId, append.Balance.Quantity, threshold.MinimumQuantity));
        }
        return new(append.Outcome, append.Balance, events);
    }

    private static InventoryCommandResult Failure(CatalogInventoryScope scope, string productId, InventoryAppendOutcome outcome, CatalogInventoryErrorCode code, string error, InventoryBalance? balance = null, InventoryMovementCommand? command = null)
    {
        var actual = balance ?? new InventoryBalance(scope.TenantId, scope.StoreId, productId, 0, 0);
        IReadOnlyList<CatalogInventoryEvent> events = command is not null && code == CatalogInventoryErrorCode.StaleVersion ? [new InventoryConflictDetectedV1(Guid.NewGuid().ToString("N"), productId, scope.TenantId, scope.StoreId, command.EffectiveAt, command.CorrelationId, productId, command.MovementId, command.ExpectedVersion, actual.Version, error)] : [];
        return new(outcome, actual, events, code, error);
    }
}