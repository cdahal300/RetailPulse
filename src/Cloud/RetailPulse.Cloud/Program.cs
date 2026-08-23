using RetailPulse.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IIdentityAuditEmitter, NoOpIdentityAuditEmitter>();
builder.Services.AddSingleton<IIdentityLifecycleService, InMemoryIdentityLifecycleService>();
builder.Services.AddSingleton<IIdentityRevocationStore, InMemoryIdentityRevocationStore>();

var app = builder.Build();
app.UseHttpsRedirection();

app.MapGet("/api/v1/me", (HttpRequest request, IIdentityRevocationStore revocations) =>
{
    if (!TryReadToken(request, out var token))
    {
        return Results.Unauthorized();
    }

    if (DateTimeOffset.UtcNow >= token.ExpiresAt || revocations.IsTokenRevoked(token.TokenId) || revocations.IsSubjectRevoked(token.TenantId, token.SubjectId))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        token.SubjectId,
        token.TenantId,
        token.StoreId,
        Roles = token.Roles.Select(role => role.ToString()).ToArray(),
        token.ExpiresAt
    });
});

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/manager/inventory-adjustments",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.AdjustInventory, auditEmitter, revocations);
        if (authorization.Result is not null)
        {
            return authorization.Result;
        }

        var token = authorization.Token!;
        return Results.Ok(new
        {
            Outcome = "Authorized",
            TenantId = tenantId,
            StoreId = storeId,
            ActorId = token.SubjectId,
            Action = AuthorizationAction.AdjustInventory.ToString()
        });
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/devices/register",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IIdentityLifecycleService lifecycle) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.RegisterDevice, auditEmitter, revocations);
        if (authorization.Result is not null)
        {
            return authorization.Result;
        }

        var actor = authorization.Token!;
        var deviceId = ReadHeader(request, "X-RetailPulse-Device-Id");
        var commandId = ReadHeader(request, "X-RetailPulse-Command-Id");
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(commandId))
        {
            return Results.BadRequest(new { Error = "X-RetailPulse-Device-Id and X-RetailPulse-Command-Id are required." });
        }

        var now = DateTimeOffset.UtcNow;
        var command = new DeviceRegistrationCommand(tenantId, storeId, deviceId, commandId, actor.SubjectId, CorrelationId(request), now);
        var result = await lifecycle.RegisterDeviceAsync(command);
        if (result.Outcome == IdentityCommandOutcome.Accepted)
        {
            return Results.Ok(new { Outcome = "Accepted", Event = result.RegisteredEvent });
        }

        return Results.Ok(new { Outcome = "Duplicate", Event = result.RegisteredEvent });
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/devices/{deviceId}/revoke",
    async (string tenantId, string storeId, string deviceId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IIdentityLifecycleService lifecycle) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.RevokeDevice, auditEmitter, revocations);
        if (authorization.Result is not null)
        {
            return authorization.Result;
        }

        var actor = authorization.Token!;
        var commandId = ReadHeader(request, "X-RetailPulse-Command-Id");
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return Results.BadRequest(new { Error = "X-RetailPulse-Command-Id is required." });
        }

        var now = DateTimeOffset.UtcNow;
        var command = new DeviceRevocationCommand(tenantId, storeId, deviceId, commandId, actor.SubjectId, CorrelationId(request), now);
        var result = await lifecycle.RevokeDeviceAsync(command);
        if (result.Outcome == IdentityCommandOutcome.NotFound)
        {
            return Results.NotFound();
        }

        revocations.RevokeSubject(tenantId, deviceId);
        return Results.Ok(new { Outcome = result.Outcome.ToString(), Event = result.RevokedEvent });
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/users/{subjectId}/roles",
    async (string tenantId, string storeId, string subjectId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IIdentityLifecycleService lifecycle) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ManageRoles, auditEmitter, revocations);
        if (authorization.Result is not null)
        {
            return authorization.Result;
        }

        var commandId = ReadHeader(request, "X-RetailPulse-Command-Id");
        var rolesRaw = ReadHeader(request, "X-RetailPulse-New-Roles");
        if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(rolesRaw) || !TryParseRoles(rolesRaw, out var roles))
        {
            return Results.BadRequest(new { Error = "X-RetailPulse-Command-Id and valid X-RetailPulse-New-Roles are required." });
        }

        var actor = authorization.Token!;
        var command = new UserRoleChangeCommand(tenantId, storeId, subjectId, roles, commandId, actor.SubjectId, CorrelationId(request), DateTimeOffset.UtcNow);
        var result = await lifecycle.ChangeUserRolesAsync(command);
        revocations.RevokeSubject(tenantId, subjectId);
        return Results.Ok(new { Outcome = result.Outcome.ToString(), Event = result.RoleChangedEvent });
    });

app.Run();

static async Task<(IResult? Result, IdentityToken? Token)> AuthorizeAsync(HttpRequest request, TenantStoreScope scope, AuthorizationAction action, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations)
{
    var correlationId = CorrelationId(request);
    var now = DateTimeOffset.UtcNow;
    if (!TryReadToken(request, out var token))
    {
        await auditEmitter.EmitTokenRejectedAsync(new(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: $"tenant:{scope.TenantId}:store:{scope.StoreId}",
            TenantId: scope.TenantId,
            StoreId: scope.StoreId,
            OccurredAt: now,
            CorrelationId: correlationId,
            SubjectId: null,
            Action: action.ToString(),
            Failure: AuthorizationFailure.InvalidToken,
            Outcome: "Rejected"));
        return (Results.Unauthorized(), null);
    }

    if (revocations.IsTokenRevoked(token.TokenId) || revocations.IsSubjectRevoked(token.TenantId, token.SubjectId))
    {
        await auditEmitter.EmitTokenRejectedAsync(new(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: token.SubjectId,
            TenantId: token.TenantId,
            StoreId: scope.StoreId,
            OccurredAt: now,
            CorrelationId: correlationId,
            SubjectId: token.SubjectId,
            Action: action.ToString(),
            Failure: AuthorizationFailure.Revoked,
            Outcome: "Rejected"));
        return (Results.Unauthorized(), null);
    }

    var decision = IdentityAuthorizationPolicy.Evaluate(token, new AuthorizationRequest(scope, action), now);
    if (decision.Allowed)
    {
        await auditEmitter.EmitPrivilegedActionAsync(new(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: token.SubjectId,
            TenantId: token.TenantId,
            StoreId: scope.StoreId,
            OccurredAt: now,
            CorrelationId: correlationId,
            SubjectId: token.SubjectId,
            PrincipalType: token.PrincipalType.ToString(),
            Action: action.ToString(),
            Outcome: "Authorized"));
        return (null, token);
    }

    await auditEmitter.EmitTokenRejectedAsync(new(
        EventId: Guid.NewGuid().ToString("N"),
        AggregateId: token.SubjectId,
        TenantId: token.TenantId,
        StoreId: scope.StoreId,
        OccurredAt: now,
        CorrelationId: correlationId,
        SubjectId: token.SubjectId,
        Action: action.ToString(),
        Failure: decision.Failure ?? AuthorizationFailure.InvalidToken,
        Outcome: "Rejected"));

    if (decision.Failure is AuthorizationFailure.ExpiredToken or AuthorizationFailure.InvalidToken or AuthorizationFailure.Revoked)
    {
        return (Results.Unauthorized(), null);
    }

    return (Results.StatusCode(StatusCodes.Status403Forbidden), null);
}

static bool TryReadToken(HttpRequest request, out IdentityToken token)
{
    token = default!;
    var tokenId = ReadHeader(request, "X-RetailPulse-Token-Id");
    var subjectId = ReadHeader(request, "X-RetailPulse-Subject-Id");
    var tenantId = ReadHeader(request, "X-RetailPulse-Tenant-Id");
    var storeId = ReadHeader(request, "X-RetailPulse-Store-Id");
    var principalTypeRaw = ReadHeader(request, "X-RetailPulse-Principal-Type");
    var rolesRaw = ReadHeader(request, "X-RetailPulse-Roles");
    var issuedAtRaw = ReadHeader(request, "X-RetailPulse-Issued-At");
    var expiresAtRaw = ReadHeader(request, "X-RetailPulse-Expires-At");

    if (string.IsNullOrWhiteSpace(tokenId) ||
        string.IsNullOrWhiteSpace(subjectId) ||
        string.IsNullOrWhiteSpace(tenantId) ||
        string.IsNullOrWhiteSpace(principalTypeRaw) ||
        string.IsNullOrWhiteSpace(rolesRaw) ||
        string.IsNullOrWhiteSpace(issuedAtRaw) ||
        string.IsNullOrWhiteSpace(expiresAtRaw))
    {
        return false;
    }

    if (!Enum.TryParse<IdentityPrincipalType>(principalTypeRaw, ignoreCase: true, out var principalType) ||
        !DateTimeOffset.TryParse(issuedAtRaw, out var issuedAt) ||
        !DateTimeOffset.TryParse(expiresAtRaw, out var expiresAt))
    {
        return false;
    }

    var parsedRoles = new List<IdentityRole>();
    foreach (var value in rolesRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        if (!Enum.TryParse<IdentityRole>(value, ignoreCase: true, out var role))
        {
            return false;
        }

        parsedRoles.Add(role);
    }

    if (parsedRoles.Count == 0)
    {
        return false;
    }

    token = new IdentityToken(tokenId, principalType, subjectId, tenantId, storeId, parsedRoles, issuedAt, expiresAt);
    return true;
}

static string? ReadHeader(HttpRequest request, string key)
{
    if (request.Headers.TryGetValue(key, out var value))
    {
        return value.ToString();
    }

    return null;
}

static string CorrelationId(HttpRequest request) => ReadHeader(request, "X-Correlation-Id") ?? Guid.NewGuid().ToString("N");

static bool TryParseRoles(string rolesRaw, out IReadOnlyCollection<IdentityRole> roles)
{
    var parsed = new List<IdentityRole>();
    foreach (var value in rolesRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        if (!Enum.TryParse<IdentityRole>(value, ignoreCase: true, out var role))
        {
            roles = [];
            return false;
        }

        parsed.Add(role);
    }

    roles = parsed;
    return parsed.Count > 0;
}
