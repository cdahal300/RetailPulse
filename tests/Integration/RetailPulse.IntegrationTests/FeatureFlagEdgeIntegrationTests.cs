using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RetailPulse.BuildingBlocks;
using RetailPulse.Cloud;
using RetailPulse.Edge;

namespace RetailPulse.IntegrationTests;

public sealed class FeatureFlagEdgeIntegrationTests
{
    [Fact]
    public async Task Edge_evaluates_signed_snapshot_offline_and_rejects_stale_versions()
    {
        const string signingKey = "edge-test-signing-key";
        await using var factory = new WebApplicationFactory<EdgeApiMarker>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<HmacFeatureFlagSnapshotSigner>();
                services.AddSingleton(new HmacFeatureFlagSnapshotSigner(Encoding.UTF8.GetBytes(signingKey)));
                services.RemoveAll<IFeatureFlagSnapshotStore>();
                services.AddSingleton<IFeatureFlagSnapshotStore, InMemoryFeatureFlagSnapshotStore>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        const string evaluationPath = "/api/v1/edge/tenants/tenant-1/stores/store-1/feature-flags/checkout.new-flow/evaluate?environment=staging&terminalId=terminal-1";

        using var noSnapshot = DeviceRequest(evaluationPath, HttpMethod.Get);
        var noSnapshotResponse = await client.SendAsync(noSnapshot);
        Assert.Equal(HttpStatusCode.OK, noSnapshotResponse.StatusCode);
        using var noSnapshotDocument = JsonDocument.Parse(await noSnapshotResponse.Content.ReadAsStringAsync());
        Assert.False(noSnapshotDocument.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal("snapshot-unavailable", noSnapshotDocument.RootElement.GetProperty("reason").GetString());

        var signer = new HmacFeatureFlagSnapshotSigner(Encoding.UTF8.GetBytes(signingKey));
        var snapshot = signer.Sign(new FeatureFlagSnapshot(
            "tenant-1",
            "store-1",
            2,
            DateTimeOffset.UtcNow.AddHours(1),
            [new FeatureFlagDefinition("checkout.new-flow", "platform", "Controlled checkout rollout", "medium", false, new FeatureFlagTargeting(Environments: new HashSet<string>(StringComparer.Ordinal) { "staging" }), DateTimeOffset.UtcNow.AddDays(1), 1)]));
        using var publish = DeviceRequest("/api/v1/edge/tenants/tenant-1/stores/store-1/feature-flag-snapshot", HttpMethod.Put);
        publish.Content = JsonContent.Create(snapshot);
        var publishResponse = await client.SendAsync(publish);
        Assert.True(publishResponse.StatusCode == HttpStatusCode.OK, await publishResponse.Content.ReadAsStringAsync());

        using var evaluation = DeviceRequest(evaluationPath, HttpMethod.Get);
        var evaluationResponse = await client.SendAsync(evaluation);
        Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        using var evaluationDocument = JsonDocument.Parse(await evaluationResponse.Content.ReadAsStringAsync());
        Assert.True(evaluationDocument.RootElement.GetProperty("enabled").GetBoolean());

        var stale = signer.Sign(snapshot with { Version = 1, Signature = string.Empty });
        using var stalePublish = DeviceRequest("/api/v1/edge/tenants/tenant-1/stores/store-1/feature-flag-snapshot", HttpMethod.Put);
        stalePublish.Content = JsonContent.Create(stale);
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(stalePublish)).StatusCode);
    }

    [Fact]
    public async Task Cloud_published_snapshot_is_accepted_and_evaluated_by_edge()
    {
        const string signingKey = "cloud-edge-signing-key";
        var signer = new HmacFeatureFlagSnapshotSigner(Encoding.UTF8.GetBytes(signingKey));
        await using var cloudFactory = new TestCloudFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<HmacFeatureFlagSnapshotSigner>();
            services.AddSingleton(signer);
        }));
        await using var edgeFactory = new WebApplicationFactory<EdgeApiMarker>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<HmacFeatureFlagSnapshotSigner>();
            services.AddSingleton(signer);
            services.RemoveAll<IFeatureFlagSnapshotStore>();
            services.AddSingleton<IFeatureFlagSnapshotStore, InMemoryFeatureFlagSnapshotStore>();
        }));
        using var cloud = cloudFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var edge = edgeFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        const string cloudFlagPath = "/api/v1/tenants/tenant-1/stores/store-1/feature-flags/checkout.new-flow";

        using var change = CloudRequest(cloudFlagPath, HttpMethod.Put, "owner-1", "Owner");
        change.Content = JsonContent.Create(CloudChangeBody());
        Assert.Equal(HttpStatusCode.OK, (await cloud.SendAsync(change)).StatusCode);
        using var approval = CloudRequest($"{cloudFlagPath}/approve", HttpMethod.Post, "owner-2", "Owner");
        approval.Content = JsonContent.Create(new { ExpectedVersion = 1, ApprovalId = "approval-1" });
        Assert.Equal(HttpStatusCode.OK, (await cloud.SendAsync(approval)).StatusCode);
        using var publish = CloudRequest("/api/v1/tenants/tenant-1/stores/store-1/feature-flag-snapshots", HttpMethod.Post, "owner-2", "Owner");
        var snapshotResponse = await cloud.SendAsync(publish);
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        var snapshotJson = await snapshotResponse.Content.ReadAsStringAsync();

        using var ingest = DeviceRequest("/api/v1/edge/tenants/tenant-1/stores/store-1/feature-flag-snapshot", HttpMethod.Put);
        ingest.Content = new StringContent(snapshotJson, Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.OK, (await edge.SendAsync(ingest)).StatusCode);
        using var evaluation = DeviceRequest("/api/v1/edge/tenants/tenant-1/stores/store-1/feature-flags/checkout.new-flow/evaluate?environment=staging&terminalId=terminal-1", HttpMethod.Get);
        var evaluationResponse = await edge.SendAsync(evaluation);
        Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        using var evaluationDocument = JsonDocument.Parse(await evaluationResponse.Content.ReadAsStringAsync());
        Assert.True(evaluationDocument.RootElement.GetProperty("enabled").GetBoolean());
    }

    private static object CloudChangeBody() => new
    {
        Draft = new
        {
            Key = "checkout.new-flow",
            Owner = "platform",
            Description = "Controlled checkout rollout",
            RiskClassification = "medium",
            Targeting = new { Environments = new[] { "staging" } },
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        },
        ExpectedVersion = 0,
        ChangeId = "change-1"
    };

    private static HttpRequestMessage CloudRequest(string path, HttpMethod method, string subjectId, string roles)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-RetailPulse-Token-Id", $"cloud-token-{subjectId}");
        request.Headers.Add("X-RetailPulse-Subject-Id", subjectId);
        request.Headers.Add("X-RetailPulse-Tenant-Id", "tenant-1");
        request.Headers.Add("X-RetailPulse-Store-Id", "store-1");
        request.Headers.Add("X-RetailPulse-Principal-Type", "User");
        request.Headers.Add("X-RetailPulse-Roles", roles);
        request.Headers.Add("X-RetailPulse-Issued-At", DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        request.Headers.Add("X-RetailPulse-Expires-At", DateTimeOffset.UtcNow.AddMinutes(30).ToString("O"));
        return request;
    }

    private static HttpRequestMessage DeviceRequest(string path, HttpMethod method)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-RetailPulse-Token-Id", "device-token");
        request.Headers.Add("X-RetailPulse-Subject-Id", "device-1");
        request.Headers.Add("X-RetailPulse-Tenant-Id", "tenant-1");
        request.Headers.Add("X-RetailPulse-Store-Id", "store-1");
        request.Headers.Add("X-RetailPulse-Principal-Type", "Device");
        request.Headers.Add("X-RetailPulse-Roles", "Device");
        request.Headers.Add("X-RetailPulse-Issued-At", DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        request.Headers.Add("X-RetailPulse-Expires-At", DateTimeOffset.UtcNow.AddMinutes(30).ToString("O"));
        return request;
    }
}