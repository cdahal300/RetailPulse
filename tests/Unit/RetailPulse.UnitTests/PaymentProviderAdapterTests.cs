using System.Net;
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

    [Fact]
    public async Task Stripe_gateway_maps_succeeded_payment_intent_and_sends_idempotency_context()
    {
        var handler = new StripeHandler("{\"id\":\"pi_test_123\",\"status\":\"succeeded\"}");
        var gateway = new StripePaymentGateway(new HttpClient(handler), "sk_test_not_used", "pm_card_visa");

        var result = await gateway.AuthorizeAsync(new Money(1250, "USD"), "USD", "store-1", "terminal-1", "transaction-1", "correlation-1", "idempotency-1");

        Assert.Equal("approved", result.Status);
        Assert.Equal("pi_test_123", result.ProviderReference);
        Assert.Equal("idempotency-1", handler.IdempotencyKey);
        Assert.Contains("amount=1250", handler.Body);
        Assert.Contains("payment_method=pm_card_visa", handler.Body);
    }

    [Fact]
    public async Task Stripe_gateway_maps_card_decline_without_exposing_error_payload()
    {
        var handler = new StripeHandler("{\"error\":{\"code\":\"card_declined\",\"message\":\"test\"}}") { StatusCode = HttpStatusCode.BadRequest };
        var gateway = new StripePaymentGateway(new HttpClient(handler), "sk_test_not_used");

        var result = await gateway.AuthorizeAsync(new Money(1250, "USD"), "USD", "store-1", "terminal-1", "transaction-1", "correlation-1", "idempotency-1");

        Assert.Equal("declined", result.Status);
        Assert.Null(result.ProviderReference);
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

    private sealed class StripeHandler(string responseBody) : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public string Body { get; private set; } = string.Empty;
        public string? IdempotencyKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(StatusCode) { Content = new StringContent(responseBody) };
        }
    }
}