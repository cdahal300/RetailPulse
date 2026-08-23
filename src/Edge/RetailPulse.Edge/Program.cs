using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

var builder = WebApplication.CreateBuilder(args);
var databasePath = builder.Configuration["RetailPulse:EdgeDatabasePath"] ?? Path.Combine(AppContext.BaseDirectory, "retailpulse-edge.db");
builder.Services.AddSingleton<ILocalCheckoutPersistence>(_ => new SqliteCheckoutPersistence(databasePath));
builder.Services.AddSingleton(_ => new BoundedAuthorizationSessionCache(TimeSpan.FromMinutes(15)));
var app = builder.Build();

app.MapGet("/", () => "RetailPulse Edge");

app.MapPost("/api/v1/edge/tenants/{tenantId}/stores/{storeId}/checkout",
	(string tenantId, string storeId, HttpRequest request, BoundedAuthorizationSessionCache cache) =>
	{
		var authorization = Authorize(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ExecuteCheckout, cache);
		if (authorization.Result is not null)
		{
			return authorization.Result;
		}

		return Results.Ok(new
		{
			Outcome = "Authorized",
			TenantId = tenantId,
			StoreId = storeId,
			ActorId = authorization.Token!.SubjectId,
			Roles = authorization.Token.Roles.Select(role => role.ToString()).ToArray()
		});
	});

app.MapPost("/api/v1/edge/tenants/{tenantId}/stores/{storeId}/inventory/adjust",
	(string tenantId, string storeId, HttpRequest request, BoundedAuthorizationSessionCache cache) =>
	{
		var authorization = Authorize(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.AdjustInventory, cache);
		if (authorization.Result is not null)
		{
			return authorization.Result;
		}

		return Results.Ok(new
		{
			Outcome = "Authorized",
			TenantId = tenantId,
			StoreId = storeId,
			ActorId = authorization.Token!.SubjectId,
			Action = AuthorizationAction.AdjustInventory.ToString()
		});
	});

app.Run();

static (IResult? Result, IdentityToken? Token) Authorize(HttpRequest request, TenantStoreScope scope, AuthorizationAction action, BoundedAuthorizationSessionCache cache)
{
	var now = DateTimeOffset.UtcNow;
	if (TryReadToken(request, out var token))
	{
		var sessionId = ReadHeader(request, "X-RetailPulse-Session-Id") ?? token.SubjectId;
		cache.Upsert(new CachedIdentitySession(sessionId, token, now, DateTimeOffset.MinValue));
		return Evaluate(scope, action, token, now);
	}

	var fallbackSessionId = ReadHeader(request, "X-RetailPulse-Session-Id");
	if (!string.IsNullOrWhiteSpace(fallbackSessionId) && cache.TryGetValid(fallbackSessionId, now, out var cached))
	{
		return Evaluate(scope, action, cached.Token, now);
	}

	return (Results.Unauthorized(), null);
}

static (IResult? Result, IdentityToken? Token) Evaluate(TenantStoreScope scope, AuthorizationAction action, IdentityToken token, DateTimeOffset now)
{
	var decision = IdentityAuthorizationPolicy.Evaluate(token, new AuthorizationRequest(scope, action), now);
	if (decision.Allowed)
	{
		return (null, token);
	}

	if (decision.Failure is AuthorizationFailure.ExpiredToken or AuthorizationFailure.InvalidToken or AuthorizationFailure.Revoked)
	{
		return (Results.Unauthorized(), null);
	}

	return (Results.Forbid(), null);
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
