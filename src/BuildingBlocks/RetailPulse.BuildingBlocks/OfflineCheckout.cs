namespace RetailPulse.BuildingBlocks;

public readonly record struct Money
{
    public Money(long minorUnits, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a three-letter code.", nameof(Currency));
        }

        MinorUnits = minorUnits;
        Currency = currency.ToUpperInvariant();
    }

    public long MinorUnits { get; }
    public string Currency { get; }

    public static Money Zero(string currency) => new(0, currency);
    public static Money operator +(Money left, Money right) => Combine(left, right, left.MinorUnits + right.MinorUnits);
    public static Money operator -(Money left, Money right) => Combine(left, right, left.MinorUnits - right.MinorUnits);
    public static Money operator *(Money value, int multiplier) => new(value.MinorUnits * multiplier, value.Currency);

    private static Money Combine(Money left, Money right, long minorUnits)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Money values must use the same currency.");
        }

        return new(minorUnits, left.Currency);
    }
}

public sealed record Product(string ProductId, string Name, Money UnitPrice, string TaxCategory, int TaxRateBasisPoints, bool IsActive = true);

public sealed record CartLine(Product Product, int Quantity)
{
    public Money Subtotal => Product.UnitPrice * Quantity;
    public Money Tax => new((long)decimal.Round(Subtotal.MinorUnits * Product.TaxRateBasisPoints / 10_000m, MidpointRounding.AwayFromZero), Subtotal.Currency);
}

public sealed class Cart
{
    private readonly List<CartLine> lines = [];

    public Cart(string cartId, string currency)
    {
        CartId = cartId;
        Currency = currency.ToUpperInvariant();
    }

    public string CartId { get; }
    public string Currency { get; }
    public IReadOnlyList<CartLine> Lines => lines;
    public Money Subtotal => lines.Aggregate(Money.Zero(Currency), (total, line) => total + line.Subtotal);
    public Money Tax => lines.Aggregate(Money.Zero(Currency), (total, line) => total + line.Tax);
    public Money Discount => Money.Zero(Currency);
    public Money Total => Subtotal + Tax - Discount;

    public void Add(Product product, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        if (!product.IsActive)
        {
            throw new InvalidOperationException("Inactive products cannot be added to a cart.");
        }

        if (!string.Equals(product.UnitPrice.Currency, Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Product currency must match the cart currency.");
        }

        lines.Add(new CartLine(product, quantity));
    }
}

public enum PaymentStatus { Approved, Declined, Cancelled, TimedOut, Pending }

public sealed class CheckoutValidationException(string message) : ArgumentException(message);

public sealed record PaymentResult(PaymentStatus Status, string? ProviderTransactionReference = null, string? AuthorizationCode = null)
{
    public static PaymentResult Approved(string reference, string? authorizationCode = null) => new(PaymentStatus.Approved, reference, authorizationCode);
    public static PaymentResult Declined() => new(PaymentStatus.Declined);
    public static PaymentResult Cancelled() => new(PaymentStatus.Cancelled);
    public static PaymentResult TimedOut() => new(PaymentStatus.TimedOut);
    public static PaymentResult Pending() => new(PaymentStatus.Pending);
}

public sealed record PaymentRequest(string TenantId, Money Amount, string StoreId, string TerminalId, string LocalTransactionId, string CorrelationId, string IdempotencyKey);

public interface IPaymentProvider
{
    Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}

public enum SaleState { Completed, PendingPayment }
public sealed record SaleLine(string ProductId, string ProductName, int Quantity, Money UnitPrice, string TaxCategory);
public sealed record Sale(string SaleId, string TenantId, string StoreId, string TerminalId, string LocalTransactionId, IReadOnlyList<SaleLine> Lines, Money Subtotal, Money Tax, Money Discount, Money Total, SaleState State, DateTimeOffset OccurredAt);
public sealed record PaymentCapture(string TenantId, PaymentStatus Status, string ProviderTransactionReference, string? AuthorizationCode);
public sealed record InventoryMovement(string ProductId, int QuantityDelta);
public sealed record ReceiptIntent(string TenantId, string SaleId);

public sealed record SaleCompletedEvent(string EventId, string AggregateId, string TenantId, string StoreId, DateTimeOffset OccurredAt, int SchemaVersion, string CorrelationId, string Source, string SaleId, string LocalTransactionId, string Currency, long TotalMinor, string PaymentReference, IReadOnlyList<InventoryMovement> InventoryMovements);
public sealed record OutboxMessage(string MessageId, string MessageType, string TenantId, string StoreId, string IdempotencyKey, DateTimeOffset OccurredAt, int SchemaVersion, SaleCompletedEvent Payload);
public sealed record CheckoutCommit(Sale Sale, PaymentCapture Payment, IReadOnlyList<InventoryMovement> InventoryMovements, ReceiptIntent ReceiptIntent, OutboxMessage OutboxMessage);

public interface ILocalCheckoutPersistence
{
    Task CommitAsync(CheckoutCommit commit, CancellationToken cancellationToken = default);
}

public readonly record struct TenantStoreScope(string TenantId, string StoreId)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantId) || string.IsNullOrWhiteSpace(StoreId))
        {
            throw new CheckoutValidationException("Tenant and store scope are required.");
        }
    }
}

public enum OutboxDeliveryState { Pending, InFlight, Retry, Synced, Review, DeadLetter }
public enum SyncAttemptClassification { Accepted, Duplicate, TransientFailure, Unauthorized, InvalidPayload, Conflict, Reviewable }
public enum SyncSubmissionOutcome { Accepted, Duplicate, Rejected, Reviewable }

public sealed record OutboxDelivery(OutboxMessage Message, int AttemptCount, OutboxDeliveryState State);
public sealed record SyncAttempt(DateTimeOffset AttemptedAt, SyncAttemptClassification Classification, string? Error = null, DateTimeOffset? RetryAt = null);
public sealed record SyncSubmissionResult(SyncSubmissionOutcome Outcome, SyncAttemptClassification Classification, string? Error = null)
{
    public bool IsSuccess => Outcome is SyncSubmissionOutcome.Accepted or SyncSubmissionOutcome.Duplicate;
}

public sealed record SyncRetryPolicy(int MaxAttempts = 5, TimeSpan? BaseDelay = null, TimeSpan? MaxDelay = null)
{
    public TimeSpan GetDelay(int attemptNumber)
    {
        if (attemptNumber < 1) throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        var baseDelay = BaseDelay ?? TimeSpan.FromSeconds(5);
        var maxDelay = MaxDelay ?? TimeSpan.FromMinutes(5);
        var seconds = Math.Min(maxDelay.TotalSeconds, baseDelay.TotalSeconds * Math.Pow(2, attemptNumber - 1));
        return TimeSpan.FromSeconds(seconds);
    }

    public bool CanRetry(int attemptNumber, SyncAttemptClassification classification) =>
        attemptNumber < MaxAttempts && classification == SyncAttemptClassification.TransientFailure;
}

public sealed record SyncHealth(int PendingCount, DateTimeOffset? OldestPendingAt, DateTimeOffset? LastSuccessAt, int RetryCount, int ConflictCount, int DeadLetterCount);

public interface IOutboxPersistence
{
    Task<IReadOnlyList<OutboxDelivery>> ClaimPendingAsync(TenantStoreScope scope, int maxMessages, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task RecordAttemptAsync(string messageId, TenantStoreScope scope, SyncAttempt attempt, CancellationToken cancellationToken = default);
    Task MarkSyncedAsync(string messageId, TenantStoreScope scope, DateTimeOffset syncedAt, CancellationToken cancellationToken = default);
    Task MarkForReviewAsync(string messageId, TenantStoreScope scope, string reason, CancellationToken cancellationToken = default);
    Task MarkDeadLetterAsync(string messageId, TenantStoreScope scope, string reason, CancellationToken cancellationToken = default);
    Task<SyncHealth> GetSyncHealthAsync(TenantStoreScope scope, CancellationToken cancellationToken = default);
}

public interface ISyncSubmissionClient
{
    Task<SyncSubmissionResult> SubmitAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public interface ISyncAuthorization
{
    bool IsAuthorized(TenantStoreScope scope, string terminalId);
}

public interface ICloudSyncHandler
{
    Task<SyncSubmissionResult> SubmitAsync(OutboxMessage message, TenantStoreScope authenticatedScope, string terminalId, CancellationToken cancellationToken = default);
}

public interface ISyncTransport
{
    Task SendAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public static class CheckoutIdempotency
{
    public static string Create(string tenantId, string storeId, string terminalId, string localTransactionId) =>
        $"v1|tenantId={Encode(tenantId)}|storeId={Encode(storeId)}|terminalId={Encode(terminalId)}|localTransactionId={Encode(localTransactionId)}";

    private static string Encode(string value) => $"{value.Length}:{value}";
}

public sealed record CheckoutRequest(Cart Cart, string TenantId, string StoreId, string TerminalId, string LocalTransactionId, string CorrelationId, DateTimeOffset? OccurredAt = null);
public enum CheckoutOutcome { Completed, Declined, Cancelled, TimedOut, Pending }
public sealed record CheckoutResult(CheckoutOutcome Outcome, PaymentResult Payment, Sale? Sale, string IdempotencyKey)
{
    public bool IsCompleted => Outcome == CheckoutOutcome.Completed;
}

public sealed class CheckoutService(IPaymentProvider paymentProvider, ILocalCheckoutPersistence persistence)
{
    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var idempotencyKey = CheckoutIdempotency.Create(request.TenantId, request.StoreId, request.TerminalId, request.LocalTransactionId);
        var payment = await paymentProvider.AuthorizeAsync(new PaymentRequest(request.TenantId, request.Cart.Total, request.StoreId, request.TerminalId, request.LocalTransactionId, request.CorrelationId, idempotencyKey), cancellationToken);
        if (payment.Status != PaymentStatus.Approved)
        {
            return new(MapOutcome(payment.Status), payment, null, idempotencyKey);
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderTransactionReference))
        {
            throw new InvalidOperationException("An approved payment must include a provider transaction reference.");
        }

        var occurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow;
        var saleId = Guid.NewGuid().ToString("N");
        var movements = request.Cart.Lines.Select(line => new InventoryMovement(line.Product.ProductId, -line.Quantity)).ToArray();
        var sale = new Sale(saleId, request.TenantId, request.StoreId, request.TerminalId, request.LocalTransactionId,
            request.Cart.Lines.Select(line => new SaleLine(line.Product.ProductId, line.Product.Name, line.Quantity, line.Product.UnitPrice, line.Product.TaxCategory)).ToArray(),
            request.Cart.Subtotal, request.Cart.Tax, request.Cart.Discount, request.Cart.Total, SaleState.Completed, occurredAt);
        var eventPayload = new SaleCompletedEvent(Guid.NewGuid().ToString("N"), saleId, request.TenantId, request.StoreId, occurredAt, 1, request.CorrelationId, "store-edge", saleId, request.LocalTransactionId, sale.Total.Currency, sale.Total.MinorUnits, payment.ProviderTransactionReference, movements);
        var outbox = new OutboxMessage(Guid.NewGuid().ToString("N"), "SaleCompleted", request.TenantId, request.StoreId, idempotencyKey, occurredAt, 1, eventPayload);
        await persistence.CommitAsync(new CheckoutCommit(sale, new PaymentCapture(request.TenantId, payment.Status, payment.ProviderTransactionReference, payment.AuthorizationCode), movements, new ReceiptIntent(request.TenantId, saleId), outbox), cancellationToken);
        return new(CheckoutOutcome.Completed, payment, sale, idempotencyKey);
    }

    private static void ValidateRequest(CheckoutRequest request)
    {
        if (request.Cart is null)
        {
            throw new CheckoutValidationException("Cart is required.");
        }

        ValidateScope(request.TenantId, nameof(request.TenantId));
        ValidateScope(request.StoreId, nameof(request.StoreId));
        ValidateScope(request.TerminalId, nameof(request.TerminalId));
        ValidateScope(request.LocalTransactionId, nameof(request.LocalTransactionId));
        ValidateScope(request.CorrelationId, nameof(request.CorrelationId));
    }

    private static void ValidateScope(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CheckoutValidationException($"{name} is required.");
        }
    }

    private static CheckoutOutcome MapOutcome(PaymentStatus status) => status switch
    {
        PaymentStatus.Declined => CheckoutOutcome.Declined,
        PaymentStatus.Cancelled => CheckoutOutcome.Cancelled,
        PaymentStatus.TimedOut => CheckoutOutcome.TimedOut,
        PaymentStatus.Pending => CheckoutOutcome.Pending,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported payment status.")
    };
}