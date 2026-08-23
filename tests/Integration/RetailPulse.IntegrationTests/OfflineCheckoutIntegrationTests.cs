using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

namespace RetailPulse.IntegrationTests;

public class OfflineCheckoutIntegrationTests
{
    [Fact]
    public async Task Edge_commit_is_local_and_contains_sale_payment_inventory_and_outbox()
    {
        var payment = new FakePaymentProvider([PaymentResult.Approved("edge-provider-ref")]);
        var persistence = new InMemoryCheckoutPersistence();
        var cart = new Cart("cart-1", "USD");
        cart.Add(new Product("sku-1", "Product", new Money(2500, "USD"), "standard", 800), 1);

        var result = await new CheckoutService(payment, persistence).CheckoutAsync(new CheckoutRequest(cart, "tenant-1", "store-1", "terminal-1", "txn-1", "correlation-1"));

        Assert.True(result.IsCompleted);
        var commit = Assert.Single(persistence.Commits);
        Assert.Equal("edge-provider-ref", commit.Payment.ProviderTransactionReference);
        Assert.Equal(commit.Sale.SaleId, commit.ReceiptIntent.SaleId);
        Assert.Single(commit.InventoryMovements);
        Assert.Equal(-1, commit.InventoryMovements[0].QuantityDelta);
        Assert.Equal("tenant-1", commit.Sale.TenantId);
        Assert.Equal("tenant-1", payment.Requests[0].TenantId);
        Assert.Equal("v1|tenantId=8:tenant-1|storeId=7:store-1|terminalId=10:terminal-1|localTransactionId=5:txn-1", commit.OutboxMessage.IdempotencyKey);
        Assert.Equal("tenant-1", commit.OutboxMessage.Payload.TenantId);
        Assert.Single(payment.Requests);
    }

    [Fact]
    public async Task Pending_payment_does_not_create_local_sale_or_outbox()
    {
        var persistence = new InMemoryCheckoutPersistence();
        var cart = new Cart("cart-1", "USD");
        cart.Add(new Product("sku-1", "Product", new Money(500, "USD"), "standard", 0), 1);

        var result = await new CheckoutService(new FakePaymentProvider([PaymentResult.Pending()]), persistence)
            .CheckoutAsync(new CheckoutRequest(cart, "tenant-1", "store-1", "terminal-1", "txn-2", "correlation-2"));

        Assert.Equal(CheckoutOutcome.Pending, result.Outcome);
        Assert.Empty(persistence.Commits);
    }
}