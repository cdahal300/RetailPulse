namespace RetailPulse.BuildingBlocks;

public sealed record InventoryAdjustmentCommand(
    string TenantId,
    string StoreId,
    string ProductId,
    int QuantityDelta,
    string Reason,
    string CommandId,
    int ExpectedVersion,
    string ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record InventoryAdjustedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string ProductId,
    int QuantityDelta,
    string Reason,
    string ActorId,
    int Version,
    int SchemaVersion = 1);

public enum ManagerCommandOutcome { Confirmed, Duplicate, Reviewable }

public sealed record InventoryAdjustmentResult(ManagerCommandOutcome Outcome, InventoryAdjustedV1? Event = null, string? Error = null);

public interface IInventoryCommandService
{
    Task<InventoryAdjustmentResult> AdjustInventoryAsync(InventoryAdjustmentCommand command, CancellationToken cancellationToken = default);
}
