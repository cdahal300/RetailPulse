namespace RetailPulse.BuildingBlocks;

public sealed record AnalyticsSalesFact(
    string SourceEventId,
    string AggregateId,
    string TenantId,
    string StoreId,
    string SaleId,
    string Currency,
    long NetSalesMinor,
    int UnitsSold,
    DateTimeOffset OccurredAt,
    int ProcessingVersion = 1);

public interface IAnalyticsFactStore
{
    Task<bool> AddAsync(AnalyticsSalesFact fact, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnalyticsSalesFact>> GetSalesFactsAsync(AnalyticsReportRequest request, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAnalyticsFactStore : IAnalyticsFactStore
{
    private readonly Dictionary<string, AnalyticsSalesFact> facts = new(StringComparer.Ordinal);

    public Task<bool> AddAsync(AnalyticsSalesFact fact, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (facts.ContainsKey(fact.SourceEventId)) return Task.FromResult(false);
        facts.Add(fact.SourceEventId, fact);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<AnalyticsSalesFact>> GetSalesFactsAsync(AnalyticsReportRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AnalyticsSalesFact> result = facts.Values
            .Where(fact => fact.TenantId == request.TenantId && fact.StoreId == request.StoreId)
            .Where(fact => fact.Currency.Equals(request.Currency, StringComparison.OrdinalIgnoreCase))
            .Where(fact => fact.OccurredAt >= request.From && fact.OccurredAt < request.To)
            .OrderBy(fact => fact.OccurredAt)
            .ToArray();
        return Task.FromResult(result);
    }
}

public sealed class AnalyticsEventIngestor(IAnalyticsFactStore store)
{
    public async Task<bool> IngestAsync(SaleCompletedEvent sourceEvent, CancellationToken cancellationToken = default)
    {
        if (sourceEvent.SchemaVersion != 1) throw new ArgumentException("Unsupported sale event schema version.", nameof(sourceEvent));
        if (string.IsNullOrWhiteSpace(sourceEvent.EventId) || string.IsNullOrWhiteSpace(sourceEvent.TenantId) || string.IsNullOrWhiteSpace(sourceEvent.StoreId))
        {
            throw new ArgumentException("Sale event ID, tenant, and store are required.", nameof(sourceEvent));
        }

        var unitsSold = Math.Max(0, -sourceEvent.InventoryMovements.Sum(movement => movement.QuantityDelta));
        return await store.AddAsync(new AnalyticsSalesFact(
            sourceEvent.EventId,
            sourceEvent.AggregateId,
            sourceEvent.TenantId,
            sourceEvent.StoreId,
            sourceEvent.SaleId,
            sourceEvent.Currency,
            sourceEvent.TotalMinor,
            unitsSold,
            sourceEvent.OccurredAt), cancellationToken);
    }
}