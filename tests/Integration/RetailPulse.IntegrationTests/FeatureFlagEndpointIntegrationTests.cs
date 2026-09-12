using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RetailPulse.IntegrationTests;

public sealed class FeatureFlagEndpointIntegrationTests
{
    [Fact]
    public async Task Cloud_flag_management_requires_separate_approval_and_publishes_signed_snapshot()
    {
        await using var factory = new TestCloudFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        const string path = "/api/v1/tenants/tenant-1/stores/store-1/feature-flags/checkout.new-flow";

        using var cashierChange = Request(path, HttpMethod.Put, "cashier-1", "Cashier");
        cashierChange.Content = JsonContent.Create(ChangeBody("change-1"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(cashierChange)).StatusCode);

        using var ownerChange = Request(path, HttpMethod.Put, "owner-1", "Owner");
        ownerChange.Content = JsonContent.Create(ChangeBody("change-1"));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(ownerChange)).StatusCode);

        using var selfApproval = Request($"{path}/approve", HttpMethod.Post, "owner-1", "Owner");
        selfApproval.Content = JsonContent.Create(new { ExpectedVersion = 1, ApprovalId = "approval-1" });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(selfApproval)).StatusCode);

        using var approval = Request($"{path}/approve", HttpMethod.Post, "owner-2", "Owner");
        approval.Content = JsonContent.Create(new { ExpectedVersion = 1, ApprovalId = "approval-2" });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(approval)).StatusCode);

        using var evaluation = Request($"{path}/evaluate?environment=staging&terminalId=terminal-1", HttpMethod.Get, "manager-1", "Manager");
        var evaluationResponse = await client.SendAsync(evaluation);
        Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        using var evaluationDocument = JsonDocument.Parse(await evaluationResponse.Content.ReadAsStringAsync());
        Assert.True(evaluationDocument.RootElement.GetProperty("enabled").GetBoolean());

        using var rollback = Request($"{path}/rollback", HttpMethod.Post, "owner-3", "Owner");
        rollback.Content = JsonContent.Create(new { ExpectedVersion = 1, RollbackId = "rollback-1" });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(rollback)).StatusCode);

        using var disabledEvaluation = Request($"{path}/evaluate?environment=staging&terminalId=terminal-1", HttpMethod.Get, "manager-1", "Manager");
        var disabledResponse = await client.SendAsync(disabledEvaluation);
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        using var disabledDocument = JsonDocument.Parse(await disabledResponse.Content.ReadAsStringAsync());
        Assert.False(disabledDocument.RootElement.GetProperty("enabled").GetBoolean());

        using var audit = Request($"{path}/audit", HttpMethod.Get, "manager-1", "Manager");
        var auditResponse = await client.SendAsync(audit);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        using var auditDocument = JsonDocument.Parse(await auditResponse.Content.ReadAsStringAsync());
        Assert.Equal(3, auditDocument.RootElement.GetArrayLength());

        using var publish = Request("/api/v1/tenants/tenant-1/stores/store-1/feature-flag-snapshots", HttpMethod.Post, "owner-2", "Owner");
        var snapshotResponse = await client.SendAsync(publish);
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        using var snapshotDocument = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(snapshotDocument.RootElement.GetProperty("signature").GetString()));
        Assert.Equal(1, snapshotDocument.RootElement.GetProperty("flags").GetArrayLength());
    }

    private static object ChangeBody(string changeId) => new
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
        ChangeId = changeId
    };

    private static HttpRequestMessage Request(string path, HttpMethod method, string subjectId, string roles)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-RetailPulse-Token-Id", $"token-{subjectId}");
        request.Headers.Add("X-RetailPulse-Subject-Id", subjectId);
        request.Headers.Add("X-RetailPulse-Tenant-Id", "tenant-1");
        request.Headers.Add("X-RetailPulse-Store-Id", "store-1");
        request.Headers.Add("X-RetailPulse-Principal-Type", "User");
        request.Headers.Add("X-RetailPulse-Roles", roles);
        request.Headers.Add("X-RetailPulse-Issued-At", DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"));
        request.Headers.Add("X-RetailPulse-Expires-At", DateTimeOffset.UtcNow.AddMinutes(30).ToString("O"));
        request.Headers.Add("X-Correlation-Id", $"corr-{subjectId}");
        return request;
    }
}