using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

namespace RetailPulse.UnitTests;

public class PaymentLifecycleServiceTests
{
    [Fact]
    public async Task Authorization_emits_an_opaque_reference_event_and_reuses_duplicate_result()
    {
        var service = new PaymentLifecycleService(new PaymentProviderAdapter(new SandboxPaymentGateway()));
        var command = new PaymentAuthorizationCommand("tenant-1", "store-1", "terminal-1", "sale-1", new Money(1000, "USD"), "correlation-1", "payment-1");

        var first = await service.AuthorizeAsync(command);
        var second = await service.AuthorizeAsync(command);

        Assert.Equal(PaymentStatus.Approved, first.Result.Status);
        Assert.StartsWith("sandbox-", first.Result.ProviderTransactionReference);
        Assert.Equal(first.Event, second.Event);
        Assert.Equal(first.Event.ProviderReference, first.Result.ProviderTransactionReference);
    }
}