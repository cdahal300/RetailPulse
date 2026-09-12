using RetailPulse.BuildingBlocks;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RetailPulse.Edge;

public sealed record ExternalPaymentAuthorization(string Status, string? ProviderReference = null, string? AuthorizationCode = null);
public sealed record PaymentAuthorizationRequest(string TerminalId, string LocalTransactionId, long AmountMinor, string Currency);

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

public sealed class StripePaymentGateway(HttpClient httpClient, string apiKey, string paymentMethodId = "pm_card_visa") : IExternalPaymentGateway
{
    public async Task<ExternalPaymentAuthorization> AuthorizeAsync(Money amount, string currency, string storeId, string terminalId, string localTransactionId, string correlationId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Stripe API key is not configured.");
        if (string.IsNullOrWhiteSpace(paymentMethodId)) throw new InvalidOperationException("Stripe test payment method is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/payment_intents");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["amount"] = amount.MinorUnits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["currency"] = currency.ToLowerInvariant(),
            ["payment_method"] = paymentMethodId,
            ["confirm"] = "true",
            ["automatic_payment_methods[enabled]"] = "true",
            ["automatic_payment_methods[allow_redirects]"] = "never",
            ["metadata[store_id]"] = storeId,
            ["metadata[terminal_id]"] = terminalId,
            ["metadata[local_transaction_id]"] = localTransactionId,
            ["metadata[correlation_id]"] = correlationId
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalPaymentAuthorization(MapStripeError(body));
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
        var paymentIntentId = root.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
        return new ExternalPaymentAuthorization(MapStripeStatus(status), paymentIntentId);
    }

    private static string MapStripeStatus(string? status) => status switch
    {
        "succeeded" => "approved",
        "requires_action" or "requires_confirmation" or "processing" => "pending",
        "canceled" => "cancelled",
        _ => "pending"
    };

    private static string MapStripeError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var code = document.RootElement.GetProperty("error").TryGetProperty("code", out var value) ? value.GetString() : null;
            return code is "card_declined" or "insufficient_funds" ? "declined" : "pending";
        }
        catch (JsonException)
        {
            return "pending";
        }
    }
}