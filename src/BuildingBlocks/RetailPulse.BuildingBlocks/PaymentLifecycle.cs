namespace RetailPulse.BuildingBlocks;

public sealed record PaymentAuthorizationCompletedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string LocalTransactionId,
    string Currency,
    long AmountMinor,
    PaymentStatus Status,
    string? ProviderReference,
    int SchemaVersion = 1);

public sealed record PaymentStatusChangedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string LocalTransactionId,
    PaymentStatus Status,
    string? ProviderReference,
    int SchemaVersion = 1);

public sealed record PaymentRefundCompletedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string LocalTransactionId,
    long AmountMinor,
    string? ProviderReference,
    int SchemaVersion = 1);

public sealed record PaymentAuthorizationCommand(
    string TenantId,
    string StoreId,
    string TerminalId,
    string LocalTransactionId,
    Money Amount,
    string CorrelationId,
    string IdempotencyKey);

public sealed record PaymentAuthorizationOutcome(PaymentResult Result, PaymentAuthorizationCompletedV1 Event);

public interface IPaymentLifecycleService
{
    Task<PaymentAuthorizationOutcome> AuthorizeAsync(PaymentAuthorizationCommand command, CancellationToken cancellationToken = default);
}