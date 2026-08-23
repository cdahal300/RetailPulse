namespace RetailPulse.BuildingBlocks;

public abstract record IdentityAuditEvent(
    string EventId,
    string AggregateId,
    string TenantId,
    string? StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    int SchemaVersion);

public sealed record PrivilegedActionAuditedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string? StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string SubjectId,
    string PrincipalType,
    string Action,
    string Outcome) : IdentityAuditEvent(EventId, AggregateId, TenantId, StoreId, OccurredAt, CorrelationId, 1);

public sealed record TokenRejectedAuditedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string? StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? SubjectId,
    string Action,
    AuthorizationFailure Failure,
    string Outcome) : IdentityAuditEvent(EventId, AggregateId, TenantId, StoreId, OccurredAt, CorrelationId, 1);

public interface IIdentityAuditEmitter
{
    Task EmitPrivilegedActionAsync(PrivilegedActionAuditedV1 auditEvent, CancellationToken cancellationToken = default);
    Task EmitTokenRejectedAsync(TokenRejectedAuditedV1 auditEvent, CancellationToken cancellationToken = default);
}

public sealed class NoOpIdentityAuditEmitter : IIdentityAuditEmitter
{
    public Task EmitPrivilegedActionAsync(PrivilegedActionAuditedV1 auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EmitTokenRejectedAsync(TokenRejectedAuditedV1 auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}