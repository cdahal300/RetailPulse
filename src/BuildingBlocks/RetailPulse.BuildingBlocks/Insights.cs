namespace RetailPulse.BuildingBlocks;

public sealed record InsightRequest(string TenantId, string StoreId, string InsightType, string RequestId, string SourceVersion);
public sealed record InsightResult(string InsightId, string TenantId, string StoreId, string InsightType, string Status, string Summary, IReadOnlyList<string> SourceReferences, string PromptVersion, string ModelDeployment, string ValidationStatus, DateTimeOffset GeneratedAt, int Version);

public interface IInsightsService
{
    Task<InsightResult> RequestAsync(InsightRequest request, CancellationToken cancellationToken = default);
    Task<InsightResult?> GetAsync(TenantStoreScope scope, string insightId, CancellationToken cancellationToken = default);
}
