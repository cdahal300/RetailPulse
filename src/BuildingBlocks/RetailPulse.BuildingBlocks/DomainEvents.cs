namespace RetailPulse.BuildingBlocks;

public interface IDomainEventPublisher
{
    Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default);
}
