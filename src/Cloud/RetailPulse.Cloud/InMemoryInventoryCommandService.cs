using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class InMemoryInventoryCommandService(CatalogInventoryService inventory) : IInventoryCommandService
{
    public Task<InventoryAdjustmentResult> AdjustInventoryAsync(InventoryAdjustmentCommand command, CancellationToken cancellationToken = default)
    {
        return AdjustCoreAsync(command, cancellationToken);
    }

    private async Task<InventoryAdjustmentResult> AdjustCoreAsync(InventoryAdjustmentCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InventoryMovementReason>(command.Reason, true, out var reason))
        {
            reason = InventoryMovementReason.Adjustment;
        }

        var result = await inventory.AdjustAsync(new InventoryMovementCommand(
            command.TenantId,
            command.StoreId,
            command.CommandId,
            command.ProductId,
            command.QuantityDelta,
            reason,
            command.OccurredAt,
            command.ExpectedVersion,
            command.ActorId,
            CatalogInventoryRole.Manager,
            command.CommandId,
            command.CorrelationId), cancellationToken);

        if (result.Outcome == InventoryAppendOutcome.Appended)
        {
            var movement = result.Events.OfType<InventoryMovementRecordedV1>().First().Movement;
            return new(ManagerCommandOutcome.Confirmed, new InventoryAdjustedV1(
                result.Events.OfType<InventoryMovementRecordedV1>().First().EventId,
                movement.ProductId,
                movement.TenantId,
                movement.StoreId,
                movement.EffectiveAt,
                movement.CorrelationId,
                movement.ProductId,
                movement.QuantityDelta,
                movement.Reason.ToString(),
                movement.ActorId,
                movement.AggregateVersion));
        }

        if (result.Outcome == InventoryAppendOutcome.Duplicate)
        {
            return new(ManagerCommandOutcome.Duplicate);
        }

        return new(ManagerCommandOutcome.Reviewable, Error: result.Error ?? "Inventory adjustment requires review.");
    }
}
