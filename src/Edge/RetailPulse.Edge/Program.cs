using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

var builder = WebApplication.CreateBuilder(args);
var databasePath = builder.Configuration["RetailPulse:EdgeDatabasePath"] ?? Path.Combine(AppContext.BaseDirectory, "retailpulse-edge.db");
builder.Services.AddSingleton<ILocalCheckoutPersistence>(_ => new SqliteCheckoutPersistence(databasePath));
builder.Services.AddSingleton(_ => new BoundedAuthorizationSessionCache(TimeSpan.FromMinutes(15)));
builder.Services.AddSingleton<IIdentityAuditEmitter, NoOpIdentityAuditEmitter>();
var app = builder.Build();

app.MapGet("/", () => "RetailPulse Edge");

app.MapPost("/api/v1/edge/tenants/{tenantId}/stores/{storeId}/checkout",
	async (string tenantId, string storeId, HttpRequest request, BoundedAuthorizationSessionCache cache, IIdentityAuditEmitter auditEmitter) =>
	{
		var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ExecuteCheckout, cache, auditEmitter);
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
	async (string tenantId, string storeId, HttpRequest request, BoundedAuthorizationSessionCache cache, IIdentityAuditEmitter auditEmitter) =>
	{
		var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.AdjustInventory, cache, auditEmitter);
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

static async Task<(IResult? Result, IdentityToken? Token)> AuthorizeAsync(HttpRequest request, TenantStoreScope scope, AuthorizationAction action, BoundedAuthorizationSessionCache cache, IIdentityAuditEmitter auditEmitter)
{
	var now = DateTimeOffset.UtcNow;
	var correlationId = CorrelationId(request);
	if (TryReadToken(request, out var token))
	{
		var sessionId = ReadHeader(request, "X-RetailPulse-Session-Id") ?? token.SubjectId;
		cache.Upsert(new CachedIdentitySession(sessionId, token, now, DateTimeOffset.MinValue));
		return await EvaluateAsync(scope, action, token, now, correlationId, auditEmitter);
	}

	var fallbackSessionId = ReadHeader(request, "X-RetailPulse-Session-Id");
	if (!string.IsNullOrWhiteSpace(fallbackSessionId) && cache.TryGetValid(fallbackSessionId, now, out var cached))
	{
		return await EvaluateAsync(scope, action, cached.Token, now, correlationId, auditEmitter);
	}

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

static async Task<(IResult? Result, IdentityToken? Token)> EvaluateAsync(TenantStoreScope scope, AuthorizationAction action, IdentityToken token, DateTimeOffset now, string correlationId, IIdentityAuditEmitter auditEmitter)
{
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

	if (decision.Failure is AuthorizationFailure.ExpiredToken or AuthorizationFailure.InvalidToken or AuthorizationFailure.Revoked)
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
			Failure: decision.Failure ?? AuthorizationFailure.InvalidToken,
			Outcome: "Rejected"));
		return (Results.Unauthorized(), null);
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
		Failure: decision.Failure ?? AuthorizationFailure.MissingRole,
		Outcome: "Rejected"));

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
