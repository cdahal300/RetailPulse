namespace RetailPulse.BuildingBlocks;

public sealed record AnalyticsReportRequest(
    string TenantId,
    string StoreId,
    DateTimeOffset From,
    DateTimeOffset To,
    string TimeZone,
    string Currency);

public sealed record AnalyticsFreshness(
    string Status,
    DateTimeOffset GeneratedAt,
    DateTimeOffset LastSourceEventAt,
    int SourceEventCount,
    int DuplicateEventCount,
    bool IsPartial,
    string DataSource);

public sealed record SalesSummaryReport(
    string TenantId,
    string StoreId,
    string Currency,
    string TimeZone,
    DateTimeOffset From,
    DateTimeOffset To,
    long NetSalesMinor,
    int OrderCount,
    int UnitsSold,
    long AverageOrderValueMinor,
    AnalyticsFreshness Freshness,
    string ReportSchemaVersion);

public sealed record HourlySalesBucket(
    DateTimeOffset Hour,
    long NetSalesMinor,
    int OrderCount,
    int UnitsSold);

public sealed record TopProductReportItem(
    string ProductId,
    string ProductName,
    int UnitsSold,
    long NetSalesMinor);

public sealed record SalesAnalyticsReport(
    SalesSummaryReport Summary,
    IReadOnlyList<HourlySalesBucket> HourlySales,
    IReadOnlyList<TopProductReportItem> TopProducts);

public interface IAnalyticsReportProvider
{
    Task<SalesAnalyticsReport> GetSalesReportAsync(AnalyticsReportRequest request, CancellationToken cancellationToken = default);
}

public sealed class SimulatedAnalyticsReportProvider : IAnalyticsReportProvider
{
    public Task<SalesAnalyticsReport> GetSalesReportAsync(AnalyticsReportRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(request);

        var facts = SampleFacts()
            .Where(fact => string.Equals(fact.TenantId, request.TenantId, StringComparison.Ordinal))
            .Where(fact => string.Equals(fact.StoreId, request.StoreId, StringComparison.Ordinal))
            .Where(fact => string.Equals(fact.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
            .Where(fact => fact.OccurredAt >= request.From && fact.OccurredAt < request.To)
            .GroupBy(fact => fact.SourceEventId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        var sourceEventCount = SampleFacts().Count(fact =>
            string.Equals(fact.TenantId, request.TenantId, StringComparison.Ordinal) &&
            string.Equals(fact.StoreId, request.StoreId, StringComparison.Ordinal) &&
            string.Equals(fact.Currency, request.Currency, StringComparison.OrdinalIgnoreCase) &&
            fact.OccurredAt >= request.From && fact.OccurredAt < request.To);
        var duplicateEventCount = sourceEventCount - facts.Length;
        var netSalesMinor = facts.Sum(fact => fact.NetSalesMinor);
        var orderCount = facts.Select(fact => fact.SaleId).Distinct(StringComparer.Ordinal).Count();
        var unitsSold = facts.Sum(fact => fact.Quantity);
        var lastSourceEventAt = facts.Length == 0 ? request.From : facts.Max(fact => fact.OccurredAt);

        var summary = new SalesSummaryReport(
            request.TenantId,
            request.StoreId,
            request.Currency.ToUpperInvariant(),
            request.TimeZone,
            request.From,
            request.To,
            netSalesMinor,
            orderCount,
            unitsSold,
            orderCount == 0 ? 0 : netSalesMinor / orderCount,
            new AnalyticsFreshness("simulated", DateTimeOffset.UtcNow, lastSourceEventAt, sourceEventCount, duplicateEventCount, IsPartial: false, DataSource: "simulated-events"),
            "sales-report.v1");

        var hourly = facts
            .GroupBy(fact => TruncateToHour(fact.OccurredAt))
            .OrderBy(group => group.Key)
            .Select(group => new HourlySalesBucket(group.Key, group.Sum(fact => fact.NetSalesMinor), group.Select(fact => fact.SaleId).Distinct(StringComparer.Ordinal).Count(), group.Sum(fact => fact.Quantity)))
            .ToArray();

        var topProducts = facts
            .GroupBy(fact => (fact.ProductId, fact.ProductName))
            .OrderByDescending(group => group.Sum(fact => fact.NetSalesMinor))
            .ThenBy(group => group.Key.ProductId, StringComparer.Ordinal)
            .Take(5)
            .Select(group => new TopProductReportItem(group.Key.ProductId, group.Key.ProductName, group.Sum(fact => fact.Quantity), group.Sum(fact => fact.NetSalesMinor)))
            .ToArray();

        return Task.FromResult(new SalesAnalyticsReport(summary, hourly, topProducts));
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

    private static IReadOnlyList<SimulatedSalesFact> SampleFacts() =>
    [
        new("event-tenant-1-store-1-001", "sale-001", "tenant-1", "store-1", "coffee", "Coffee", 2, 2000, "USD", new DateTimeOffset(2026, 08, 23, 14, 05, 00, TimeSpan.Zero)),
        new("event-tenant-1-store-1-002", "sale-002", "tenant-1", "store-1", "tea", "Tea", 1, 1200, "USD", new DateTimeOffset(2026, 08, 23, 14, 25, 00, TimeSpan.Zero)),
        new("event-tenant-1-store-1-003", "sale-003", "tenant-1", "store-1", "sandwich", "Sandwich", 3, 2550, "USD", new DateTimeOffset(2026, 08, 23, 15, 10, 00, TimeSpan.Zero)),
        new("event-tenant-1-store-1-003", "sale-003", "tenant-1", "store-1", "sandwich", "Sandwich", 3, 2550, "USD", new DateTimeOffset(2026, 08, 23, 15, 10, 00, TimeSpan.Zero)),
        new("event-tenant-1-store-2-001", "sale-004", "tenant-1", "store-2", "coffee", "Coffee", 1, 1000, "USD", new DateTimeOffset(2026, 08, 23, 14, 40, 00, TimeSpan.Zero)),
        new("event-tenant-2-store-1-001", "sale-005", "tenant-2", "store-1", "coffee", "Coffee", 5, 5000, "USD", new DateTimeOffset(2026, 08, 23, 14, 50, 00, TimeSpan.Zero))
    ];

    private sealed record SimulatedSalesFact(
        string SourceEventId,
        string SaleId,
        string TenantId,
        string StoreId,
        string ProductId,
        string ProductName,
        int Quantity,
        long NetSalesMinor,
        string Currency,
        DateTimeOffset OccurredAt);
}