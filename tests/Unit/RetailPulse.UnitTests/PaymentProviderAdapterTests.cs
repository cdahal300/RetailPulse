using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

namespace RetailPulse.UnitTests;

public class PaymentProviderAdapterTests
{
    [Theory]
    [InlineData("approved", PaymentStatus.Approved)]
    [InlineData("declined", PaymentStatus.Declined)]
    [InlineData("pending", PaymentStatus.Pending)]
    [InlineData("timeout", PaymentStatus.TimedOut)]
    public async Task Adapter_maps_external_statuses_without_card_data(string status, PaymentStatus expected)
    {
        var adapter = new PaymentProviderAdapter(new StubGateway(status));
        var result = await adapter.AuthorizeAsync(Request("transaction-1", "idempotency-1"));

        Assert.Equal(expected, result.Status);
        Assert.DoesNotContain("card", string.Join('|', result.ProviderTransactionReference ?? ""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_idempotency_key_does_not_call_gateway_twice()
    {
        var gateway = new StubGateway("approved");
        var adapter = new PaymentProviderAdapter(gateway);
        var request = Request("transaction-1", "idempotency-1");

        await adapter.AuthorizeAsync(request);
        await adapter.AuthorizeAsync(request);

        Assert.Equal(1, gateway.CallCount);
    }

    private static PaymentRequest Request(string transactionId, string idempotencyKey) => new("tenant-1", new Money(1250, "USD"), "store-1", "terminal-1", transactionId, "correlation-1", idempotencyKey);

    private sealed class StubGateway(string status) : IExternalPaymentGateway
    {
        public int CallCount { get; private set; }

        public Task<ExternalPaymentAuthorization> AuthorizeAsync(Money amount, string currency, string storeId, string terminalId, string localTransactionId, string correlationId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ExternalPaymentAuthorization(status, status == "approved" ? "opaque-reference" : null));
        }
    }
}