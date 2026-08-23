using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class OfflineCheckoutTests
{
    [Fact]
    public void Cart_calculates_subtotal_tax_and_total_in_minor_units()
    {
        var cart = new Cart("cart-1", "usd");
        cart.Add(new Product("coffee", "Coffee", new Money(1000, "USD"), "standard", 750), 2);

        Assert.Equal(new Money(2000, "USD"), cart.Subtotal);
        Assert.Equal(new Money(150, "USD"), cart.Tax);
        Assert.Equal(new Money(2150, "USD"), cart.Total);
    }

    [Fact]
    public async Task Approved_payment_commits_sale_and_outbox_once()
    {
        var persistence = new TestPersistence();
        var result = await CreateService(new TestPaymentProvider(PaymentResult.Approved("provider-123", "auth-1")), persistence)
            .CheckoutAsync(CreateRequest());

        Assert.True(result.IsCompleted);
        Assert.Single(persistence.Commits);
        Assert.Equal("provider-123", persistence.Commits[0].Payment.ProviderTransactionReference);
        Assert.Equal("tenant-001", persistence.Commits[0].Sale.TenantId);
        Assert.Equal("tenant-001", persistence.Commits[0].Payment.TenantId);
        Assert.Equal(SaleState.Completed, persistence.Commits[0].Sale.State);
        Assert.Equal(persistence.Commits[0].Sale.SaleId, persistence.Commits[0].ReceiptIntent.SaleId);
    }

    [Theory]
    [InlineData(PaymentStatus.Declined, CheckoutOutcome.Declined)]
    [InlineData(PaymentStatus.Cancelled, CheckoutOutcome.Cancelled)]
    [InlineData(PaymentStatus.TimedOut, CheckoutOutcome.TimedOut)]
    [InlineData(PaymentStatus.Pending, CheckoutOutcome.Pending)]
    public async Task Non_approved_payment_is_a_result_and_does_not_commit(PaymentStatus status, CheckoutOutcome outcome)
    {
        var persistence = new TestPersistence();
        var result = await CreateService(new TestPaymentProvider(new PaymentResult(status)), persistence)
            .CheckoutAsync(CreateRequest());

        Assert.Equal(outcome, result.Outcome);
        Assert.Empty(persistence.Commits);
        Assert.Null(result.Sale);
    }

    [Fact]
    public void Idempotency_key_is_stable_for_checkout_identity()
    {
        Assert.Equal("v1|tenantId=10:tenant-001|storeId=9:store-001|terminalId=11:terminal-02|localTransactionId=15:local-txn-10452", CheckoutIdempotency.Create("tenant-001", "store-001", "terminal-02", "local-txn-10452"));
        Assert.NotEqual(CheckoutIdempotency.Create("tenant-a", "store-001", "terminal-02", "local-txn-10452"), CheckoutIdempotency.Create("tenant-b", "store-001", "terminal-02", "local-txn-10452"));
        Assert.NotEqual(CheckoutIdempotency.Create("tenant-001", "store-1", "terminal-02", "local-txn-10452"), CheckoutIdempotency.Create("tenant-001", "store", "terminal-02", "local-txn-10452"));
    }

    [Fact]
    public async Task Outbox_contains_versioned_sale_completed_event_and_inventory_movements()
    {
        var persistence = new TestPersistence();
        await CreateService(new TestPaymentProvider(PaymentResult.Approved("provider-123")), persistence)
            .CheckoutAsync(CreateRequest());

        var outbox = Assert.Single(persistence.Commits).OutboxMessage;
        Assert.Equal("SaleCompleted", outbox.MessageType);
        Assert.Equal(1, outbox.SchemaVersion);
        Assert.Equal("v1|tenantId=10:tenant-001|storeId=9:store-001|terminalId=11:terminal-02|localTransactionId=15:local-txn-10452", outbox.IdempotencyKey);
        Assert.Equal("tenant-001", outbox.TenantId);
        Assert.Equal("tenant-001", outbox.Payload.TenantId);
        Assert.Equal("provider-123", outbox.Payload.PaymentReference);
        Assert.Equal(-2, Assert.Single(outbox.Payload.InventoryMovements).QuantityDelta);
    }

    [Theory]
    [InlineData("TenantId")]
    [InlineData("StoreId")]
    [InlineData("TerminalId")]
    [InlineData("LocalTransactionId")]
    [InlineData("CorrelationId")]
    public async Task Missing_checkout_scope_is_rejected_before_payment_authorization(string missingScope)
    {
        var payment = new TestPaymentProvider(PaymentResult.Approved("provider-123"));
        var request = CreateRequest();
        request = missingScope switch
        {
            "TenantId" => request with { TenantId = " " },
            "StoreId" => request with { StoreId = "" },
            "TerminalId" => request with { TerminalId = "\t" },
            "LocalTransactionId" => request with { LocalTransactionId = " " },
            "CorrelationId" => request with { CorrelationId = "" },
            _ => request
        };

        var exception = await Assert.ThrowsAsync<CheckoutValidationException>(() => CreateService(payment, new TestPersistence()).CheckoutAsync(request));

        Assert.Contains(missingScope, exception.Message);
        Assert.Null(payment.Request);
    }

    [Fact]
    public async Task Payment_request_carries_tenant_and_correlation_scope()
    {
        var payment = new TestPaymentProvider(PaymentResult.Declined());

        await CreateService(payment, new TestPersistence()).CheckoutAsync(CreateRequest());

        Assert.Equal("tenant-001", payment.Request?.TenantId);
        Assert.Equal("correlation-1", payment.Request?.CorrelationId);
    }

    private static CheckoutService CreateService(TestPaymentProvider provider, TestPersistence persistence) => new(provider, persistence);

    private static CheckoutRequest CreateRequest()
    {
        var cart = new Cart("cart-1", "USD");
        cart.Add(new Product("coffee", "Coffee", new Money(1000, "USD"), "standard", 0), 2);
        return new(cart, "tenant-001", "store-001", "terminal-02", "local-txn-10452", "correlation-1", DateTimeOffset.UnixEpoch);
    }

    private sealed class TestPaymentProvider(PaymentResult result) : IPaymentProvider
    {
        public PaymentRequest? Request { get; private set; }

        public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class TestPersistence : ILocalCheckoutPersistence
    {
        public List<CheckoutCommit> Commits { get; } = [];
        public Task CommitAsync(CheckoutCommit commit, CancellationToken cancellationToken = default) { Commits.Add(commit); return Task.CompletedTask; }
    }
}