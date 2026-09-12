using RetailPulse.BuildingBlocks;

namespace RetailPulse.Edge;

public sealed record ExternalPaymentAuthorization(string Status, string? ProviderReference = null, string? AuthorizationCode = null);

public interface IExternalPaymentGateway
{
    Task<ExternalPaymentAuthorization> AuthorizeAsync(
        Money amount,
        string currency,
        string storeId,
        string terminalId,
        string localTransactionId,
        string correlationId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public sealed class PaymentProviderAdapter(IExternalPaymentGateway gateway) : IPaymentProvider
{
    private readonly Dictionary<string, PaymentResult> results = new(StringComparer.Ordinal);

    public async Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        if (results.TryGetValue(request.IdempotencyKey, out var existing)) return existing;

        var response = await gateway.AuthorizeAsync(
            request.Amount,
            request.Amount.Currency,
            request.StoreId,
            request.TerminalId,
            request.LocalTransactionId,
            request.CorrelationId,
            request.IdempotencyKey,
            cancellationToken);
        var result = Map(response);
        if (result.Status is PaymentStatus.Approved or PaymentStatus.Declined or PaymentStatus.Cancelled or PaymentStatus.Pending)
        {
            results[request.IdempotencyKey] = result;
        }
        return result;
    }

    private static PaymentResult Map(ExternalPaymentAuthorization response) => response.Status.ToLowerInvariant() switch
    {
        "approved" => PaymentResult.Approved(RequireReference(response), response.AuthorizationCode),
        "declined" => PaymentResult.Declined(),
        "cancelled" or "canceled" => PaymentResult.Cancelled(),
        "pending" => PaymentResult.Pending(),
        "timeout" or "timedout" => PaymentResult.TimedOut(),
        _ => PaymentResult.Pending()
    };

    private static string RequireReference(ExternalPaymentAuthorization response) =>
        string.IsNullOrWhiteSpace(response.ProviderReference)
            ? throw new InvalidOperationException("Approved payment responses require an opaque provider reference.")
            : response.ProviderReference;

    private static void Validate(PaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.StoreId) || string.IsNullOrWhiteSpace(request.TerminalId) || string.IsNullOrWhiteSpace(request.LocalTransactionId) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("Tenant, store, terminal, transaction, and idempotency context are required.", nameof(request));
        }
        if (request.Amount.MinorUnits <= 0) throw new ArgumentException("Payment amount must be positive.", nameof(request));
    }
}

public sealed class SandboxPaymentGateway : IExternalPaymentGateway
{
    public Task<ExternalPaymentAuthorization> AuthorizeAsync(Money amount, string currency, string storeId, string terminalId, string localTransactionId, string correlationId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = localTransactionId.Contains("decline", StringComparison.OrdinalIgnoreCase) ? "declined" :
            localTransactionId.Contains("pending", StringComparison.OrdinalIgnoreCase) ? "pending" :
            localTransactionId.Contains("timeout", StringComparison.OrdinalIgnoreCase) ? "timeout" : "approved";
        return Task.FromResult(new ExternalPaymentAuthorization(status, status == "approved" ? $"sandbox-{idempotencyKey}" : null, status == "approved" ? "sandbox-auth" : null));
    }
}