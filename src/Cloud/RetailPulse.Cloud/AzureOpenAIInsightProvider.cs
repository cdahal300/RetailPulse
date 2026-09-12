using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class AzureOpenAIInsightProvider : IInsightProvider
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;

    public AzureOpenAIInsightProvider(IConfiguration? configuration = null, HttpClient? httpClient = null)
    {
        this.configuration = configuration ?? new ConfigurationBuilder().AddEnvironmentVariables().Build();
        this.httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public AzureOpenAIInsightProvider(IConfiguration? configuration, HttpMessageHandler handler)
        : this(configuration, new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        })
    {
    }

    public async Task<InsightProviderResult> GenerateAsync(InsightModelRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = configuration["AzureOpenAI:Endpoint"];
        var apiKey = configuration["AzureOpenAI:ApiKey"];
        var deployment = configuration["AzureOpenAI:Deployment"];
        var promptVersion = configuration["AzureOpenAI:PromptVersion"] ?? request.PromptVersion;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
        {
            return new InsightProviderResult("Unavailable", "Azure OpenAI is not configured.", "Unavailable", "not-configured", promptVersion);
        }

        try
        {
            var sanitizedPrompt = InsightGuardrails.Validate(request.PromptText, request.SourceVersion);
            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You are a retail analytics assistant. Return a short, plain-language summary of the provided sales activity. Never include payment card numbers, CVV, PINs, raw card data, or customer identifiers." },
                    new { role = "user", content = sanitizedPrompt }
                },
                max_tokens = 250,
                temperature = 0.2
            };

            var requestUri = $"{endpoint.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version=2024-06-01";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(requestBody)
            };
            httpRequest.Headers.Add("api-key", apiKey);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new InsightProviderResult("Unavailable", null, "Unavailable", "rate-limited", promptVersion);
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new InsightProviderResult("Unavailable", null, "Unavailable", "provider-unavailable", promptVersion);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new InsightProviderResult("Unavailable", null, "Unavailable", "provider-unavailable", promptVersion);
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(payload);
            var content = ExtractContent(document.RootElement);

            if (string.IsNullOrWhiteSpace(content))
            {
                return new InsightProviderResult("Reviewable", null, "Failed", "invalid-json", promptVersion);
            }

            var sanitizedResponse = InsightGuardrails.Sanitize(content);
            if (string.IsNullOrWhiteSpace(sanitizedResponse) || sanitizedResponse.Contains("[unsafe instruction redacted]", StringComparison.OrdinalIgnoreCase))
            {
                return new InsightProviderResult("Reviewable", null, "Failed", "unsafe-output", promptVersion);
            }

            return new InsightProviderResult("Completed", sanitizedResponse, "Validated", string.Empty, promptVersion);
        }
        catch (TaskCanceledException)
        {
            return new InsightProviderResult("Unavailable", null, "Unavailable", "timeout", promptVersion);
        }
        catch (JsonException)
        {
            return new InsightProviderResult("Reviewable", null, "Failed", "invalid-json", promptVersion);
        }
        catch (Exception)
        {
            return new InsightProviderResult("Unavailable", null, "Unavailable", "provider-unavailable", promptVersion);
        }
    }

    private static string? ExtractContent(JsonElement root)
    {
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString();
                }
            }
        }

        return null;
    }
}
