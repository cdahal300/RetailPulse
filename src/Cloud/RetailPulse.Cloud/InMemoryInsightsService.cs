using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class InMemoryInsightsService(IAnalyticsReportProvider reports, IInsightProvider? provider = null, string? modelDeployment = null) : IInsightsService
{
    private readonly Dictionary<string, InsightResult> insights = [];
    private readonly Dictionary<string, string> requestIds = [];
    private readonly Dictionary<string, InsightJob> jobs = [];
    private readonly IInsightProvider provider = provider ?? new DeterministicInsightProvider();
    private readonly string modelDeployment = modelDeployment ?? "not-configured";

    public async Task<InsightResult> RequestAsync(InsightRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.TenantId)) throw new ArgumentException("Tenant is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.StoreId)) throw new ArgumentException("Store is required.", nameof(request));
        if (request.InsightType is not "sales-summary") throw new ArgumentException("Only sales-summary insights are supported.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestId)) throw new ArgumentException("Request ID is required.", nameof(request));

        var sourceVersion = InsightGuardrails.Validate(request.SourceVersion, request.SourceVersion);
        var dedupeKey = BuildDedupeKey(request.RequestId, request.SourceVersion);
        if (requestIds.TryGetValue(dedupeKey, out var existingId)) return insights[existingId];

        var createdAt = DateTimeOffset.UtcNow;
        var job = new InsightJob(Guid.NewGuid().ToString("N"), request.TenantId, request.StoreId, request.InsightType, request.RequestId, request.SourceVersion, "Queued", null, createdAt, null, null);
        jobs[BuildJobKey(request.TenantId, request.StoreId, request.RequestId, request.SourceVersion)] = job;

        var report = await reports.GetSalesReportAsync(new(request.TenantId, request.StoreId, DateTimeOffset.Parse("2026-08-23T00:00:00Z"), DateTimeOffset.Parse("2026-08-24T00:00:00Z"), "UTC", "USD"), cancellationToken);
        var topProduct = report.TopProducts.FirstOrDefault();
        var draftSummary = topProduct is null
            ? "No sales activity was available for this source window."
            : $"Net sales were {report.Summary.NetSalesMinor / 100m:C} across {report.Summary.OrderCount} orders. {topProduct.ProductName} led with {topProduct.UnitsSold} units. Review inventory coverage before the next trading window.";
        var summary = InsightGuardrails.Validate(draftSummary, request.SourceVersion);
        var diagnostics = await provider.GenerateAsync(new InsightModelRequest(request.TenantId, request.StoreId, request.InsightType, request.SourceVersion, "deterministic-v1", summary), cancellationToken);

        var status = string.Equals(diagnostics.Status, "Reviewable", StringComparison.OrdinalIgnoreCase)
            ? "Reviewable"
            : string.Equals(diagnostics.Status, "Unavailable", StringComparison.OrdinalIgnoreCase)
                ? "Unavailable"
                : "Completed";
        var validationStatus = string.Equals(diagnostics.ValidationStatus, "Failed", StringComparison.OrdinalIgnoreCase)
            ? "Failed"
            : string.Equals(diagnostics.ValidationStatus, "Unavailable", StringComparison.OrdinalIgnoreCase)
                ? "Unavailable"
                : "Validated";

        var result = new InsightResult(Guid.NewGuid().ToString("N"), request.TenantId, request.StoreId, request.InsightType, status, diagnostics.Summary ?? summary, [$"sales-report:{request.SourceVersion}", $"store:{request.StoreId}"], diagnostics.PromptVersion, modelDeployment, validationStatus, DateTimeOffset.UtcNow, 1);

        insights[result.InsightId] = result;
        requestIds[dedupeKey] = result.InsightId;
        jobs[BuildJobKey(request.TenantId, request.StoreId, request.RequestId, request.SourceVersion)] = job with { Status = status, InsightId = result.InsightId, CompletedAt = DateTimeOffset.UtcNow, ValidationStatus = result.ValidationStatus };
        return result;
    }

    public Task<InsightResult?> GetAsync(TenantStoreScope scope, string insightId, CancellationToken cancellationToken = default) =>
        Task.FromResult(insights.GetValueOrDefault(insightId) is { } result && result.TenantId == scope.TenantId && result.StoreId == scope.StoreId ? result : null);

    public Task<InsightJob?> GetJobAsync(TenantStoreScope scope, string requestId, string? sourceVersion = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildJobKey(scope.TenantId, scope.StoreId, requestId, sourceVersion ?? string.Empty);
        return Task.FromResult(jobs.TryGetValue(key, out var job) && job.TenantId == scope.TenantId && job.StoreId == scope.StoreId ? job : null);
    }

    private static string BuildDedupeKey(string requestId, string sourceVersion) =>
        string.Concat(requestId, "|", sourceVersion);

    private static string BuildJobKey(string tenantId, string storeId, string requestId, string? sourceVersion) =>
        string.Concat(tenantId, "|", storeId, "|", requestId, "|", sourceVersion ?? string.Empty);
}
