using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RetailPulse.BuildingBlocks;
using RetailPulse.Cloud;
using RetailPulse.Edge;

namespace RetailPulse.IntegrationTests;

public sealed class IdentityAuthorizationEndpointIntegrationTests : IClassFixture<WebApplicationFactory<CloudApiMarker>>, IClassFixture<WebApplicationFactory<EdgeApiMarker>>
{
    private readonly HttpClient cloudClient;
    private readonly HttpClient edgeClient;

    public IdentityAuthorizationEndpointIntegrationTests(WebApplicationFactory<CloudApiMarker> cloudFactory, WebApplicationFactory<EdgeApiMarker> edgeFactory)
    {
        cloudClient = cloudFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        edgeClient = edgeFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    [Fact]
    public async Task Cloud_manager_endpoint_returns_expected_auth_status_codes()
    {
        var noToken = await cloudClient.PostAsync("/api/v1/tenants/tenant-1/stores/store-1/manager/inventory-adjustments", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

        using var cashierRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/manager/inventory-adjustments",
            tokenId: "cashier-token",
            subjectId: "cashier-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Cashier",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        var cashier = await cloudClient.SendAsync(cashierRequest);
        Assert.Equal(HttpStatusCode.Forbidden, cashier.StatusCode);

        using var managerRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/manager/inventory-adjustments",
            tokenId: "manager-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        var manager = await cloudClient.SendAsync(managerRequest);
        Assert.Equal(HttpStatusCode.OK, manager.StatusCode);
    }

    [Fact]
    public async Task Cloud_rejects_expired_and_cross_store_tokens()
    {
        using var expiredRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/manager/inventory-adjustments",
            tokenId: "expired-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddHours(-2),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var expired = await cloudClient.SendAsync(expiredRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        using var wrongStoreRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-2/manager/inventory-adjustments",
            tokenId: "manager-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        var wrongStore = await cloudClient.SendAsync(wrongStoreRequest);
        Assert.Equal(HttpStatusCode.Forbidden, wrongStore.StatusCode);
    }

    [Fact]
    public async Task Cloud_device_registration_requires_owner_role()
    {
        using var ownerRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/devices/register",
            tokenId: "owner-token",
            subjectId: "owner-1",
            tenantId: "tenant-1",
            storeId: null,
            principalType: "User",
            roles: "Owner",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        ownerRequest.Headers.Add("X-RetailPulse-Device-Id", "device-1");
        ownerRequest.Headers.Add("X-RetailPulse-Command-Id", "register-device-1");
        var owner = await cloudClient.SendAsync(ownerRequest);
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);

        using var deviceRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/devices/register",
            tokenId: "device-token",
            subjectId: "device-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "Device",
            roles: "Device",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        deviceRequest.Headers.Add("X-RetailPulse-Device-Id", "device-2");
        deviceRequest.Headers.Add("X-RetailPulse-Command-Id", "register-device-2");
        var device = await cloudClient.SendAsync(deviceRequest);
        Assert.Equal(HttpStatusCode.Forbidden, device.StatusCode);
    }

    [Fact]
    public async Task Cloud_role_change_revokes_existing_subject_session()
    {
        await using var factory = new WebApplicationFactory<CloudApiMarker>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var roleChangeRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/users/manager-1/roles",
            tokenId: "owner-token",
            subjectId: "owner-1",
            tenantId: "tenant-1",
            storeId: null,
            principalType: "User",
            roles: "Owner",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        roleChangeRequest.Headers.Add("X-RetailPulse-Command-Id", "role-change-1");
        roleChangeRequest.Headers.Add("X-RetailPulse-New-Roles", "Cashier");
        var roleChange = await client.SendAsync(roleChangeRequest);
        Assert.Equal(HttpStatusCode.OK, roleChange.StatusCode);

        using var managerRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/manager/inventory-adjustments",
            tokenId: "manager-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        var managerAfterRoleChange = await client.SendAsync(managerRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, managerAfterRoleChange.StatusCode);
    }

    [Fact]
    public async Task Edge_checkout_and_adjust_enforce_role_and_scope()
    {
        using var checkoutRequest = EdgeRequest(
            "/api/v1/edge/tenants/tenant-1/stores/store-1/checkout",
            tokenId: "cashier-token",
            subjectId: "cashier-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Cashier",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
            sessionId: "cashier-session");
        var checkout = await edgeClient.SendAsync(checkoutRequest);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        using var adjustAsCashierRequest = EdgeRequest(
            "/api/v1/edge/tenants/tenant-1/stores/store-1/inventory/adjust",
            tokenId: "cashier-token",
            subjectId: "cashier-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Cashier",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
            sessionId: "cashier-session");
        var adjustAsCashier = await edgeClient.SendAsync(adjustAsCashierRequest);
        Assert.Equal(HttpStatusCode.Forbidden, adjustAsCashier.StatusCode);

        using var wrongStoreRequest = EdgeRequest(
            "/api/v1/edge/tenants/tenant-1/stores/store-2/inventory/adjust",
            tokenId: "manager-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
            sessionId: "manager-session");
        var wrongStore = await edgeClient.SendAsync(wrongStoreRequest);
        Assert.Equal(HttpStatusCode.Forbidden, wrongStore.StatusCode);
    }

    [Fact]
    public async Task Edge_rejects_expired_token_and_allows_bounded_cached_session()
    {
        await using var factory = new WebApplicationFactory<EdgeApiMarker>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var expiredRequest = EdgeRequest(
            "/api/v1/edge/tenants/tenant-1/stores/store-1/inventory/adjust",
            tokenId: "expired-manager-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddHours(-2),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            sessionId: "expired-session");
        var expired = await client.SendAsync(expiredRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        using var primeSessionRequest = EdgeRequest(
            "/api/v1/edge/tenants/tenant-1/stores/store-1/checkout",
            tokenId: "manager-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
            sessionId: "offline-session");
        var primed = await client.SendAsync(primeSessionRequest);
        Assert.Equal(HttpStatusCode.OK, primed.StatusCode);

        using var cachedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/edge/tenants/tenant-1/stores/store-1/checkout");
        cachedRequest.Headers.Add("X-RetailPulse-Session-Id", "offline-session");
        var cached = await client.SendAsync(cachedRequest);
        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);

        using var revokeRequest = EdgeRequest(
            "/api/v1/edge/tenants/tenant-1/stores/store-1/identity/revoke-subject/manager-1",
            tokenId: "owner-token",
            subjectId: "owner-1",
            tenantId: "tenant-1",
            storeId: null,
            principalType: "User",
            roles: "Owner",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30),
            sessionId: "owner-session");
        var revokeResult = await client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.Accepted, revokeResult.StatusCode);

        using var cachedAfterRevoke = new HttpRequestMessage(HttpMethod.Post, "/api/v1/edge/tenants/tenant-1/stores/store-1/checkout");
        cachedAfterRevoke.Headers.Add("X-RetailPulse-Session-Id", "offline-session");
        var rejectedAfterRevoke = await client.SendAsync(cachedAfterRevoke);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedAfterRevoke.StatusCode);
    }

    [Fact]
    public async Task Cloud_emits_audit_events_for_authorized_and_rejected_requests()
    {
        var auditEmitter = new InMemoryIdentityAuditEmitter();
        await using var factory = new WebApplicationFactory<CloudApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityAuditEmitter>();
                services.AddSingleton<IIdentityAuditEmitter>(auditEmitter);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var managerRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/manager/inventory-adjustments",
            tokenId: "manager-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        managerRequest.Headers.Add("X-Correlation-Id", "corr-ok-1");
        var managerResult = await client.SendAsync(managerRequest);

        using var cashierRequest = CloudRequest(
            "/api/v1/tenants/tenant-1/stores/store-1/manager/inventory-adjustments",
            tokenId: "cashier-token",
            subjectId: "cashier-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Cashier",
            issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));
        cashierRequest.Headers.Add("X-Correlation-Id", "corr-denied-1");
        var cashierResult = await client.SendAsync(cashierRequest);

        Assert.Equal(HttpStatusCode.OK, managerResult.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cashierResult.StatusCode);

        var authorizedEvent = Assert.Single(auditEmitter.PrivilegedActions);
        Assert.Equal("manager-1", authorizedEvent.SubjectId);
        Assert.Equal("AdjustInventory", authorizedEvent.Action);
        Assert.Equal("corr-ok-1", authorizedEvent.CorrelationId);

        var rejectedEvent = Assert.Single(auditEmitter.TokenRejections);
        Assert.Equal("cashier-1", rejectedEvent.SubjectId);
        Assert.Equal("AdjustInventory", rejectedEvent.Action);
        Assert.Equal(AuthorizationFailure.MissingRole, rejectedEvent.Failure);
        Assert.Equal("corr-denied-1", rejectedEvent.CorrelationId);
    }

    [Fact]
    public async Task Edge_emits_rejection_audit_for_expired_token()
    {
        var auditEmitter = new InMemoryIdentityAuditEmitter();
        await using var factory = new WebApplicationFactory<EdgeApiMarker>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityAuditEmitter>();
                services.AddSingleton<IIdentityAuditEmitter>(auditEmitter);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var expiredRequest = EdgeRequest(
            "/api/v1/edge/tenants/tenant-1/stores/store-1/inventory/adjust",
            tokenId: "expired-token",
            subjectId: "manager-1",
            tenantId: "tenant-1",
            storeId: "store-1",
            principalType: "User",
            roles: "Manager",
            issuedAt: DateTimeOffset.UtcNow.AddHours(-2),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            sessionId: "expired-session");
        expiredRequest.Headers.Add("X-Correlation-Id", "corr-expired-edge");

        var result = await client.SendAsync(expiredRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        var rejection = Assert.Single(auditEmitter.TokenRejections);
        Assert.Equal("manager-1", rejection.SubjectId);
        Assert.Equal(AuthorizationFailure.ExpiredToken, rejection.Failure);
        Assert.Equal("AdjustInventory", rejection.Action);
        Assert.Equal("corr-expired-edge", rejection.CorrelationId);
    }

    private static HttpRequestMessage CloudRequest(
        string path,
        string tokenId,
        string subjectId,
        string tenantId,
        string? storeId,
        string principalType,
        string roles,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-RetailPulse-Token-Id", tokenId);
        request.Headers.Add("X-RetailPulse-Subject-Id", subjectId);
        request.Headers.Add("X-RetailPulse-Tenant-Id", tenantId);
        if (!string.IsNullOrWhiteSpace(storeId))
        {
            request.Headers.Add("X-RetailPulse-Store-Id", storeId);
        }

        request.Headers.Add("X-RetailPulse-Principal-Type", principalType);
        request.Headers.Add("X-RetailPulse-Roles", roles);
        request.Headers.Add("X-RetailPulse-Issued-At", issuedAt.ToString("O"));
        request.Headers.Add("X-RetailPulse-Expires-At", expiresAt.ToString("O"));
        return request;
    }

    private static HttpRequestMessage EdgeRequest(
        string path,
        string tokenId,
        string subjectId,
        string tenantId,
        string? storeId,
        string principalType,
        string roles,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string sessionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-RetailPulse-Token-Id", tokenId);
        request.Headers.Add("X-RetailPulse-Subject-Id", subjectId);
        request.Headers.Add("X-RetailPulse-Tenant-Id", tenantId);
        if (!string.IsNullOrWhiteSpace(storeId))
        {
            request.Headers.Add("X-RetailPulse-Store-Id", storeId);
        }

        request.Headers.Add("X-RetailPulse-Principal-Type", principalType);
        request.Headers.Add("X-RetailPulse-Roles", roles);
        request.Headers.Add("X-RetailPulse-Issued-At", issuedAt.ToString("O"));
        request.Headers.Add("X-RetailPulse-Expires-At", expiresAt.ToString("O"));
        request.Headers.Add("X-RetailPulse-Session-Id", sessionId);
        return request;
    }
}

public sealed class InMemoryIdentityAuditEmitter : IIdentityAuditEmitter
{
    private readonly List<PrivilegedActionAuditedV1> privilegedActions = [];
    private readonly List<TokenRejectedAuditedV1> tokenRejections = [];
    private readonly object gate = new();

    public IReadOnlyList<PrivilegedActionAuditedV1> PrivilegedActions
    {
        get
        {
            lock (gate)
            {
                return privilegedActions.ToArray();
            }
        }
    }

    public IReadOnlyList<TokenRejectedAuditedV1> TokenRejections
    {
        get
        {
            lock (gate)
            {
                return tokenRejections.ToArray();
            }
        }
    }

    public Task EmitPrivilegedActionAsync(PrivilegedActionAuditedV1 auditEvent, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            privilegedActions.Add(auditEvent);
        }

        return Task.CompletedTask;
    }

    public Task EmitTokenRejectedAsync(TokenRejectedAuditedV1 auditEvent, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            tokenRejections.Add(auditEvent);
        }

        return Task.CompletedTask;
    }
}