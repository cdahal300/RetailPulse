using RetailPulse.BuildingBlocks;

namespace RetailPulse.Edge;

public sealed class PaymentLifecycleService(IPaymentProvider provider) : IPaymentLifecycleService
{
    private readonly Dictionary<string, PaymentAuthorizationOutcome> outcomes = new(StringComparer.Ordinal);

    public async Task<PaymentAuthorizationOutcome> AuthorizeAsync(PaymentAuthorizationCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.TenantId) || string.IsNullOrWhiteSpace(command.StoreId) || string.IsNullOrWhiteSpace(command.TerminalId) || string.IsNullOrWhiteSpace(command.LocalTransactionId) || string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new ArgumentException("Payment command context is incomplete.", nameof(command));
        }
        if (outcomes.TryGetValue(command.IdempotencyKey, out var existing)) return existing;

        var result = await provider.AuthorizeAsync(new PaymentRequest(command.TenantId, command.Amount, command.StoreId, command.TerminalId, command.LocalTransactionId, command.CorrelationId, command.IdempotencyKey), cancellationToken);
        var paymentEvent = new PaymentAuthorizationCompletedV1(
            Guid.NewGuid().ToString("N"),
            command.LocalTransactionId,
            command.TenantId,
            command.StoreId,
            DateTimeOffset.UtcNow,
            command.CorrelationId,
            command.LocalTransactionId,
            command.Amount.Currency,
            command.Amount.MinorUnits,
            result.Status,
            result.ProviderTransactionReference);
        var outcome = new PaymentAuthorizationOutcome(result, paymentEvent);
        if (result.Status is not PaymentStatus.TimedOut) outcomes[command.IdempotencyKey] = outcome;
        return outcome;
    }
}