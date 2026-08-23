using RetailPulse.BuildingBlocks;

var builder = WebApplication.CreateBuilder(args);

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
    (string tenantId, string storeId, HttpRequest request) =>
    {
        if (!TryReadToken(request, out var token))
        {
            return Results.Unauthorized();
        }

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new TenantStoreScope(tenantId, storeId), AuthorizationAction.AdjustInventory),
            DateTimeOffset.UtcNow);

        if (decision.Allowed)
        {
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
            return Results.Unauthorized();
        }

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/devices/register",
    (string tenantId, string storeId, HttpRequest request) =>
    {
        if (!TryReadToken(request, out var token))
        {
            return Results.Unauthorized();
        }

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new TenantStoreScope(tenantId, storeId), AuthorizationAction.RegisterDevice),
            DateTimeOffset.UtcNow);

        if (decision.Allowed)
        {
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
            return Results.Unauthorized();
        }

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
