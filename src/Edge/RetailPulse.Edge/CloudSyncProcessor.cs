using RetailPulse.BuildingBlocks;

namespace RetailPulse.Edge;

public sealed class SyncTransportException(string message, SyncAttemptClassification classification = SyncAttemptClassification.TransientFailure) : Exception(message)
{
    public SyncAttemptClassification Classification { get; } = classification;
}

public sealed class SyncTransportSubmissionClient(ISyncTransport transport) : ISyncSubmissionClient
{
    public async Task<SyncSubmissionResult> SubmitAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await transport.SendAsync(message, cancellationToken);
            return new(SyncSubmissionOutcome.Accepted, SyncAttemptClassification.Accepted);
        }
        catch (SyncTransportException exception)
        {
            return new(SyncSubmissionOutcome.Rejected, exception.Classification, exception.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(SyncSubmissionOutcome.Rejected, SyncAttemptClassification.TransientFailure, "Sync transport timed out.");
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            return new(SyncSubmissionOutcome.Rejected, SyncAttemptClassification.TransientFailure, exception.Message);
        }
    }
}

public sealed class OutboxSyncProcessor(IOutboxPersistence persistence, ISyncSubmissionClient client, SyncRetryPolicy? retryPolicy = null)
{
    private readonly SyncRetryPolicy retryPolicy = retryPolicy ?? new();

    public async Task<int> ProcessAsync(TenantStoreScope scope, int batchSize = 20, DateTimeOffset? now = null, CancellationToken cancellationToken = default)
    {
        scope.Validate();
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var currentTime = now ?? DateTimeOffset.UtcNow;
        var deliveries = await persistence.ClaimPendingAsync(scope, batchSize, currentTime, cancellationToken);
        foreach (var delivery in deliveries)
        {
            var result = await client.SubmitAsync(delivery.Message, cancellationToken);
            var attemptNumber = delivery.AttemptCount + 1;
            DateTimeOffset? retryAt = retryPolicy.CanRetry(attemptNumber, result.Classification) ? currentTime + retryPolicy.GetDelay(attemptNumber) : null;
            var attempt = new SyncAttempt(currentTime, result.Classification, result.Error, retryAt);
            await persistence.RecordAttemptAsync(delivery.Message.MessageId, scope, attempt, cancellationToken);
            if (result.IsSuccess)
            {
                await persistence.MarkSyncedAsync(delivery.Message.MessageId, scope, currentTime, cancellationToken);
                continue;
            }

            if (retryPolicy.CanRetry(attemptNumber, result.Classification))
            {
                continue;
            }

            if (result.Classification is SyncAttemptClassification.InvalidPayload or SyncAttemptClassification.Unauthorized or SyncAttemptClassification.Conflict || attemptNumber >= retryPolicy.MaxAttempts)
            {
                if (result.Classification == SyncAttemptClassification.Conflict)
                {
                    await persistence.MarkForReviewAsync(delivery.Message.MessageId, scope, result.Error ?? "Sync conflict.", cancellationToken);
                }
                else
                {
                    await persistence.MarkDeadLetterAsync(delivery.Message.MessageId, scope, result.Error ?? "Sync delivery failed.", cancellationToken);
                }
            }
        }

        return deliveries.Count;
    }
}
