namespace RetailPulse.BuildingBlocks;

public sealed record AnalyticsReplayResult(string ReplayId, string CommandId, bool Applied);

public sealed class AnalyticsReplayService(AnalyticsEventIngestor ingestor)
{
    private readonly Dictionary<string, AnalyticsReplayResult> results = new(StringComparer.Ordinal);

    public async Task<AnalyticsReplayResult> ReplayAsync(string commandId, SaleCompletedEvent sourceEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandId)) throw new ArgumentException("Command ID is required.", nameof(commandId));
        if (results.TryGetValue(commandId, out var existing)) return existing;

        var applied = await ingestor.CorrectAsync(sourceEvent, cancellationToken);
        var result = new AnalyticsReplayResult(Guid.NewGuid().ToString("N"), commandId, applied);
        results[commandId] = result;
        return result;
    }
}