using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class InMemoryInsightsService(IAnalyticsReportProvider reports) : IInsightsService
{
    private readonly Dictionary<string, InsightResult> insights = [];
    private readonly Dictionary<string, string> requestIds = [];

    public async Task<InsightResult> RequestAsync(InsightRequest request, CancellationToken cancellationToken = default)
    {
        if (request.InsightType is not "sales-summary") throw new ArgumentException("Only sales-summary insights are supported.", nameof(request));
        if (requestIds.TryGetValue(request.RequestId, out var existingId)) return insights[existingId];

        var report = await reports.GetSalesReportAsync(new(request.TenantId, request.StoreId, DateTimeOffset.Parse("2026-08-23T00:00:00Z"), DateTimeOffset.Parse("2026-08-24T00:00:00Z"), "UTC", "USD"), cancellationToken);
        var topProduct = report.TopProducts.FirstOrDefault();
        var summary = topProduct is null
            ? "No sales activity was available for this source window."
            : $"Net sales were {report.Summary.NetSalesMinor / 100m:C} across {report.Summary.OrderCount} orders. {topProduct.ProductName} led with {topProduct.UnitsSold} units. Review inventory coverage before the next trading window.";
        var result = new InsightResult(Guid.NewGuid().ToString("N"), request.TenantId, request.StoreId, request.InsightType, "Completed", summary, [$"sales-report:{request.SourceVersion}", $"store:{request.StoreId}"], "deterministic-v1", "not-configured", "Validated", DateTimeOffset.UtcNow, 1);
        insights[result.InsightId] = result;
        requestIds[request.RequestId] = result.InsightId;
        return result;
    }

    public Task<InsightResult?> GetAsync(TenantStoreScope scope, string insightId, CancellationToken cancellationToken = default) =>
        Task.FromResult(insights.GetValueOrDefault(insightId) is { } result && result.TenantId == scope.TenantId && result.StoreId == scope.StoreId ? result : null);
}
