namespace RetailPulse.BuildingBlocks;

public sealed record InsightRequest(string TenantId, string StoreId, string InsightType, string RequestId, string SourceVersion);
public sealed record InsightResult(string InsightId, string TenantId, string StoreId, string InsightType, string Status, string Summary, IReadOnlyList<string> SourceReferences, string PromptVersion, string ModelDeployment, string ValidationStatus, DateTimeOffset GeneratedAt, int Version);
public sealed record InsightJob(string JobId, string TenantId, string StoreId, string InsightType, string RequestId, string SourceVersion, string Status, string? InsightId, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? ValidationStatus);
public sealed record InsightModelRequest(string TenantId, string StoreId, string InsightType, string SourceVersion, string PromptVersion, string PromptText);
public sealed record InsightProviderResult(string Status, string? Summary, string ValidationStatus, string ErrorCode, string PromptVersion);

public interface IInsightProvider
{
    Task<InsightProviderResult> GenerateAsync(InsightModelRequest request, CancellationToken cancellationToken = default);
}

public static class InsightGuardrails
{
    public static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var value = input;
        value = System.Text.RegularExpressions.Regex.Replace(value, @"(?i)(?<![A-Za-z])card\s*(?:number|info|details)?\s*[:=]?\s*[0-9][0-9\s-]{10,}|\b(?:pan|cvv|pin)\b\s*[:=]?\s*[0-9A-Za-z-]+", " [redacted card data] ", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        value = System.Text.RegularExpressions.Regex.Replace(value, @"(?i)\b(?:cvv|pin|magnetic stripe|payment method)\b.*", " [redacted payment data] ", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        value = System.Text.RegularExpressions.Regex.Replace(value, @"(?i)ignore previous instructions|system prompt|developer message|override the rules|act as admin|bypass authorization", "[unsafe instruction redacted]", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return value.Trim();
    }

    public static string Validate(string input, string sourceVersion)
    {
        if (string.IsNullOrWhiteSpace(sourceVersion)) throw new ArgumentException("Source version is required.", nameof(sourceVersion));
        if (!IsSupportedSourceVersion(sourceVersion)) throw new ArgumentException("Source version is stale or unsupported.", nameof(sourceVersion));

        var cleaned = Sanitize(input);
        if (cleaned.Contains("ignore previous instructions", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Contains("override the rules", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Contains("bypass authorization", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Contains("[unsafe instruction redacted]", StringComparison.OrdinalIgnoreCase) ||
            cleaned.Contains("card", StringComparison.OrdinalIgnoreCase) && cleaned.Contains("approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Insight payload is unsafe and was rejected.", nameof(input));
        }

        return cleaned;
    }

    public static bool IsSupportedSourceVersion(string sourceVersion) =>
        string.Equals(sourceVersion, "sales-report.v1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sourceVersion, "sales-report.v2", StringComparison.OrdinalIgnoreCase);
}

public interface IInsightsService
{
    Task<InsightResult> RequestAsync(InsightRequest request, CancellationToken cancellationToken = default);
    Task<InsightResult?> GetAsync(TenantStoreScope scope, string insightId, CancellationToken cancellationToken = default);
    Task<InsightJob?> GetJobAsync(TenantStoreScope scope, string requestId, string? sourceVersion = null, CancellationToken cancellationToken = default);
}

public sealed class DeterministicInsightProvider : IInsightProvider
{
    public Task<InsightProviderResult> GenerateAsync(InsightModelRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new InsightProviderResult("Completed", request.PromptText, "Validated", "", request.PromptVersion));
    }
}
