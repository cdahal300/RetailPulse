using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class InMemorySyncAuthorization : ISyncAuthorization
{
    private readonly HashSet<(string TenantId, string StoreId, string TerminalId)> registrations = [];

    public void Register(TenantStoreScope scope, string terminalId)
    {
        scope.Validate();
        if (string.IsNullOrWhiteSpace(terminalId)) throw new ArgumentException("Terminal is required.", nameof(terminalId));
        registrations.Add((scope.TenantId, scope.StoreId, terminalId));
    }

    public bool IsAuthorized(TenantStoreScope scope, string terminalId) => registrations.Contains((scope.TenantId, scope.StoreId, terminalId));
}

public sealed class InMemoryCloudSyncHandler(ISyncAuthorization authorization) : ICloudSyncHandler
{
    private readonly Dictionary<string, (TenantStoreScope Scope, SyncSubmissionResult Result)> accepted = [];
    private readonly HashSet<string> eventIds = [];
    public int AppliedSaleCount { get; private set; }

    public Task<SyncSubmissionResult> SubmitAsync(OutboxMessage message, TenantStoreScope authenticatedScope, string terminalId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        authenticatedScope.Validate();
        if (!string.Equals(message.MessageType, "SaleCompleted", StringComparison.Ordinal) || message.Payload is null)
        {
            return Task.FromResult(new SyncSubmissionResult(SyncSubmissionOutcome.Rejected, SyncAttemptClassification.InvalidPayload, "Only SaleCompleted messages are supported."));
        }

        if (!string.Equals(message.TenantId, authenticatedScope.TenantId, StringComparison.Ordinal) || !string.Equals(message.StoreId, authenticatedScope.StoreId, StringComparison.Ordinal) ||
            !string.Equals(message.Payload.TenantId, authenticatedScope.TenantId, StringComparison.Ordinal) || !string.Equals(message.Payload.StoreId, authenticatedScope.StoreId, StringComparison.Ordinal) ||
            !authorization.IsAuthorized(authenticatedScope, terminalId) || !string.Equals(message.Payload.Source, "store-edge", StringComparison.Ordinal))
        {
            return Task.FromResult(new SyncSubmissionResult(SyncSubmissionOutcome.Rejected, SyncAttemptClassification.Unauthorized, "Message scope is not authorized."));
        }

        if (accepted.TryGetValue(message.IdempotencyKey, out var existing))
        {
            return Task.FromResult(existing.Scope == authenticatedScope
                ? new SyncSubmissionResult(SyncSubmissionOutcome.Duplicate, SyncAttemptClassification.Duplicate)
                : new SyncSubmissionResult(SyncSubmissionOutcome.Rejected, SyncAttemptClassification.Unauthorized, "Message scope is not authorized."));
        }

        if (eventIds.Contains(message.Payload.EventId))
        {
            return Task.FromResult(new SyncSubmissionResult(SyncSubmissionOutcome.Duplicate, SyncAttemptClassification.Duplicate));
        }

        var result = new SyncSubmissionResult(SyncSubmissionOutcome.Accepted, SyncAttemptClassification.Accepted);
        accepted[message.IdempotencyKey] = (authenticatedScope, result);
        eventIds.Add(message.Payload.EventId);
        AppliedSaleCount++;
        return Task.FromResult(result);
    }
}
