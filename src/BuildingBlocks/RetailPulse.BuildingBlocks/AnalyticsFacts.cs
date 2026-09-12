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
    Task<bool> CorrectAsync(AnalyticsSalesFact fact, CancellationToken cancellationToken = default);
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

    public Task<bool> CorrectAsync(AnalyticsSalesFact fact, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existed = facts.ContainsKey(fact.SourceEventId);
        facts[fact.SourceEventId] = fact;
        return Task.FromResult(existed);
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

    public async Task<bool> CorrectAsync(SaleCompletedEvent sourceEvent, CancellationToken cancellationToken = default)
    {
        if (sourceEvent.SchemaVersion != 1) throw new ArgumentException("Unsupported sale event schema version.", nameof(sourceEvent));
        var unitsSold = Math.Max(0, -sourceEvent.InventoryMovements.Sum(movement => movement.QuantityDelta));
        return await store.CorrectAsync(new AnalyticsSalesFact(
            sourceEvent.EventId,
            sourceEvent.AggregateId,
            sourceEvent.TenantId,
            sourceEvent.StoreId,
            sourceEvent.SaleId,
            sourceEvent.Currency,
            sourceEvent.TotalMinor,
            unitsSold,
            sourceEvent.OccurredAt,
            ProcessingVersion: 2), cancellationToken);
    }
}

public sealed class EventAnalyticsReportProvider(IAnalyticsFactStore store) : IAnalyticsReportProvider
{
    public async Task<SalesAnalyticsReport> GetSalesReportAsync(AnalyticsReportRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var facts = await store.GetSalesFactsAsync(request, cancellationToken);
        var distinctSales = facts.Select(fact => fact.SaleId).Distinct(StringComparer.Ordinal).ToArray();
        var netSalesMinor = facts.Sum(fact => fact.NetSalesMinor);
        var lastSourceEventAt = facts.Count == 0 ? request.From : facts.Max(fact => fact.OccurredAt);
        var hourlySales = facts
            .GroupBy(fact => TruncateToHour(fact.OccurredAt))
            .OrderBy(group => group.Key)
            .Select(group => new HourlySalesBucket(
                group.Key,
                group.Sum(fact => fact.NetSalesMinor),
                group.Select(fact => fact.SaleId).Distinct(StringComparer.Ordinal).Count(),
                group.Sum(fact => fact.UnitsSold)))
            .ToArray();

        var summary = new SalesSummaryReport(
            request.TenantId,
            request.StoreId,
            request.Currency.ToUpperInvariant(),
            request.TimeZone,
            request.From,
            request.To,
            netSalesMinor,
            distinctSales.Length,
            facts.Sum(fact => fact.UnitsSold),
            distinctSales.Length == 0 ? 0 : netSalesMinor / distinctSales.Length,
            new AnalyticsFreshness("complete", DateTimeOffset.UtcNow, lastSourceEventAt, facts.Count, 0, false, "event-facts"),
            "sales-report.v1");

        return new SalesAnalyticsReport(summary, hourlySales, []);
    }

    private static void Validate(AnalyticsReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId)) throw new ArgumentException("Tenant is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.StoreId)) throw new ArgumentException("Store is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TimeZone)) throw new ArgumentException("Timezone is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Currency)) throw new ArgumentException("Currency is required.", nameof(request));
        if (request.To <= request.From) throw new ArgumentException("Report end time must be after start time.", nameof(request));
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset value) => new(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Offset);
}