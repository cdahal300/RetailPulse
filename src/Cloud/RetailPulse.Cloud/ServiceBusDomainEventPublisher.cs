using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class NoOpDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class ServiceBusDomainEventPublisher : IDomainEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient client;
    private readonly ServiceBusSender sender;

    public ServiceBusDomainEventPublisher(string fullyQualifiedNamespace, string topicName = "retailpulse-events")
    {
        client = new ServiceBusClient(fullyQualifiedNamespace, new DefaultAzureCredential());
        sender = client.CreateSender(topicName);
    }

    public async Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload);
        using var document = JsonDocument.Parse(json);
        var eventId = document.RootElement.TryGetProperty("eventId", out var eventIdValue) ? eventIdValue.GetString() : null;
        var message = new ServiceBusMessage(json)
        {
            Subject = eventType,
            ContentType = "application/json",
            MessageId = eventId ?? Guid.NewGuid().ToString("N")
        };
        await sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await sender.DisposeAsync();
        await client.DisposeAsync();
    }
}
