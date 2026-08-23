using RetailPulse.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IIdentityAuditEmitter, NoOpIdentityAuditEmitter>();

var app = builder.Build();
app.UseHttpsRedirection();

app.MapGet("/api/v1/me", (HttpRequest request) =>
{
    if (!TryReadToken(request, out var token))
    {
        return Results.Unauthorized();
    }

    if (DateTimeOffset.UtcNow >= token.ExpiresAt)
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
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter) =>
    {
        var correlationId = CorrelationId(request);
        var now = DateTimeOffset.UtcNow;
        if (!TryReadToken(request, out var token))
        {
            await auditEmitter.EmitTokenRejectedAsync(new(
                EventId: Guid.NewGuid().ToString("N"),
                AggregateId: $"tenant:{tenantId}:store:{storeId}",
                TenantId: tenantId,
                StoreId: storeId,
                OccurredAt: now,
                CorrelationId: correlationId,
                SubjectId: null,
                Action: AuthorizationAction.AdjustInventory.ToString(),
                Failure: AuthorizationFailure.InvalidToken,
                Outcome: "Rejected"));
            return Results.Unauthorized();
        }

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new TenantStoreScope(tenantId, storeId), AuthorizationAction.AdjustInventory),
            now);

        if (decision.Allowed)
        {
            await auditEmitter.EmitPrivilegedActionAsync(new(
                EventId: Guid.NewGuid().ToString("N"),
                AggregateId: token.SubjectId,
                TenantId: token.TenantId,
                StoreId: storeId,
                OccurredAt: now,
                CorrelationId: correlationId,
                SubjectId: token.SubjectId,
                PrincipalType: token.PrincipalType.ToString(),
                Action: AuthorizationAction.AdjustInventory.ToString(),
                Outcome: "Authorized"));
            return Results.Ok(new
            {
                Outcome = "Authorized",
                TenantId = tenantId,
                StoreId = storeId,
                ActorId = token.SubjectId,
                Action = AuthorizationAction.AdjustInventory.ToString()
            });
        }

        if (decision.Failure is AuthorizationFailure.ExpiredToken or AuthorizationFailure.InvalidToken or AuthorizationFailure.Revoked)
        {
            await auditEmitter.EmitTokenRejectedAsync(new(
                EventId: Guid.NewGuid().ToString("N"),
                AggregateId: token.SubjectId,
                TenantId: token.TenantId,
                StoreId: storeId,
                OccurredAt: now,
                CorrelationId: correlationId,
                SubjectId: token.SubjectId,
                Action: AuthorizationAction.AdjustInventory.ToString(),
                Failure: decision.Failure ?? AuthorizationFailure.InvalidToken,
                Outcome: "Rejected"));
            return Results.Unauthorized();
        }

        await auditEmitter.EmitTokenRejectedAsync(new(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: token.SubjectId,
            TenantId: token.TenantId,
            StoreId: storeId,
            OccurredAt: now,
            CorrelationId: correlationId,
            SubjectId: token.SubjectId,
            Action: AuthorizationAction.AdjustInventory.ToString(),
            Failure: decision.Failure ?? AuthorizationFailure.MissingRole,
            Outcome: "Rejected"));
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/devices/register",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter) =>
    {
        var correlationId = CorrelationId(request);
        var now = DateTimeOffset.UtcNow;
        if (!TryReadToken(request, out var token))
        {
            await auditEmitter.EmitTokenRejectedAsync(new(
                EventId: Guid.NewGuid().ToString("N"),
                AggregateId: $"tenant:{tenantId}:store:{storeId}",
                TenantId: tenantId,
                StoreId: storeId,
                OccurredAt: now,
                CorrelationId: correlationId,
                SubjectId: null,
                Action: AuthorizationAction.RegisterDevice.ToString(),
                Failure: AuthorizationFailure.InvalidToken,
                Outcome: "Rejected"));
            return Results.Unauthorized();
        }

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new TenantStoreScope(tenantId, storeId), AuthorizationAction.RegisterDevice),
            now);

        if (decision.Allowed)
        {
            await auditEmitter.EmitPrivilegedActionAsync(new(
                EventId: Guid.NewGuid().ToString("N"),
                AggregateId: token.SubjectId,
                TenantId: token.TenantId,
                StoreId: storeId,
                OccurredAt: now,
                CorrelationId: correlationId,
                SubjectId: token.SubjectId,
                PrincipalType: token.PrincipalType.ToString(),
                Action: AuthorizationAction.RegisterDevice.ToString(),
                Outcome: "Authorized"));
            return Results.Ok(new
            {
                Outcome = "Authorized",
                TenantId = tenantId,
                StoreId = storeId,
                ActorId = token.SubjectId,
                Action = AuthorizationAction.RegisterDevice.ToString()
            });
        }

        if (decision.Failure is AuthorizationFailure.ExpiredToken or AuthorizationFailure.InvalidToken or AuthorizationFailure.Revoked)
        {
            await auditEmitter.EmitTokenRejectedAsync(new(
                EventId: Guid.NewGuid().ToString("N"),
                AggregateId: token.SubjectId,
                TenantId: token.TenantId,
                StoreId: storeId,
                OccurredAt: now,
                CorrelationId: correlationId,
                SubjectId: token.SubjectId,
                Action: AuthorizationAction.RegisterDevice.ToString(),
                Failure: decision.Failure ?? AuthorizationFailure.InvalidToken,
                Outcome: "Rejected"));
            return Results.Unauthorized();
        }

        await auditEmitter.EmitTokenRejectedAsync(new(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: token.SubjectId,
            TenantId: token.TenantId,
            StoreId: storeId,
            OccurredAt: now,
            CorrelationId: correlationId,
            SubjectId: token.SubjectId,
            Action: AuthorizationAction.RegisterDevice.ToString(),
            Failure: decision.Failure ?? AuthorizationFailure.MissingRole,
            Outcome: "Rejected"));
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    });

app.Run();

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
