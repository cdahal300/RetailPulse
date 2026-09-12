using RetailPulse.BuildingBlocks;
using RetailPulse.Cloud;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Azure.Identity;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var keyVaultUri = builder.Configuration["AzureKeyVault:VaultUri"];
if (Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var keyVaultEndpoint))
{
    builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential());
}
var entraTenantId = builder.Configuration["Entra:TenantId"];
var entraAudience = builder.Configuration["Entra:Audience"];
var entraConfigured = !string.IsNullOrWhiteSpace(entraTenantId) && !string.IsNullOrWhiteSpace(entraAudience);
var portalAllowedOrigins = builder.Configuration["Portal:AllowedOrigins"]?
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

if (entraConfigured)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://login.microsoftonline.com/{entraTenantId}/v2.0";
            options.Audience = entraAudience;
            options.RequireHttpsMetadata = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters.RoleClaimType = "roles";
            options.TokenValidationParameters.NameClaimType = "name";
        });
    builder.Services.AddAuthorization();
}

if (portalAllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy("Portal", policy =>
        policy.WithOrigins(portalAllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
}

var cloudDatabasePath = builder.Configuration["RetailPulse:CloudDatabasePath"] ?? Path.Combine(AppContext.BaseDirectory, "retailpulse-cloud.db");
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
var useSqliteCloudLedger = builder.Configuration.GetValue<bool>("RetailPulse:UseSqliteCloudLedger");
var pushVapidPublicKey = builder.Configuration["Push:VapidPublicKey"];
var pushVapidPrivateKey = builder.Configuration["Push:VapidPrivateKey"];
var serviceBusNamespace = builder.Configuration["ServiceBus:FullyQualifiedNamespace"];
var useEventAnalytics = builder.Configuration.GetValue<bool>("Analytics:UseEventFacts");
builder.Services.AddSingleton<IPushNotificationQueue, PushNotificationQueue>();
builder.Services.AddSingleton<IIdentityAuditEmitter>(_ => useSqliteCloudLedger || string.IsNullOrWhiteSpace(postgresConnectionString)
    ? new NoOpIdentityAuditEmitter()
    : new PostgresIdentityAuditEmitter(postgresConnectionString));
builder.Services.AddSingleton<IIdentityLifecycleService>(_ => useSqliteCloudLedger || string.IsNullOrWhiteSpace(postgresConnectionString)
    ? new InMemoryIdentityLifecycleService()
    : new PostgresIdentityLifecycleService(postgresConnectionString));
builder.Services.AddSingleton<IIdentityRevocationStore>(_ => useSqliteCloudLedger || string.IsNullOrWhiteSpace(postgresConnectionString)
    ? new InMemoryIdentityRevocationStore()
    : new PostgresIdentityRevocationStore(postgresConnectionString));
builder.Services.AddSingleton<IDomainEventPublisher>(services => string.IsNullOrWhiteSpace(serviceBusNamespace)
    ? new NoOpDomainEventPublisher()
    : new ServiceBusDomainEventPublisher(serviceBusNamespace, services.GetRequiredService<IPushNotificationQueue>(), services.GetRequiredService<AnalyticsEventIngestor>()));
builder.Services.AddSingleton<ICatalogRepository, CloudCatalogRepository>();
builder.Services.AddSingleton<IInventoryLedgerRepository>(_ => useSqliteCloudLedger || string.IsNullOrWhiteSpace(postgresConnectionString)
    ? new SqliteInventoryLedger(cloudDatabasePath)
    : new PostgresInventoryLedger(postgresConnectionString));
builder.Services.AddSingleton<ICatalogInventoryAuthorization, CloudInventoryAuthorization>();
builder.Services.AddSingleton<CatalogInventoryService>();
builder.Services.AddSingleton<IInventoryCommandService>(services => new InMemoryInventoryCommandService(
    services.GetRequiredService<CatalogInventoryService>(),
    services.GetRequiredService<IDomainEventPublisher>()));
builder.Services.AddSingleton<ISyncHealthReader>(_ => new PostgresSyncHealthReader(useSqliteCloudLedger ? null : postgresConnectionString));
builder.Services.AddSingleton<IAlertsReader>(_ => new PostgresAlertsReader(useSqliteCloudLedger ? null : postgresConnectionString));
builder.Services.AddSingleton<IStoreSettingsRepository>(_ => new PostgresStoreSettingsRepository(useSqliteCloudLedger ? null : postgresConnectionString));
builder.Services.AddSingleton<IAnalyticsFactStore>(_ => useSqliteCloudLedger || string.IsNullOrWhiteSpace(postgresConnectionString)
    ? new InMemoryAnalyticsFactStore()
    : new PostgresAnalyticsFactStore(postgresConnectionString));
builder.Services.AddSingleton<AnalyticsEventIngestor>();
builder.Services.AddSingleton<AnalyticsReplayService>();
builder.Services.AddSingleton<IInsightsService, InMemoryInsightsService>();
builder.Services.AddSingleton<IPushSubscriptionStore>(_ => new PostgresPushSubscriptionStore(useSqliteCloudLedger ? null : postgresConnectionString));
builder.Services.AddSingleton<IPushNotificationSender>(_ =>
    string.IsNullOrWhiteSpace(pushVapidPublicKey) || string.IsNullOrWhiteSpace(pushVapidPrivateKey)
        ? new NoOpPushNotificationSender()
        : new VapidPushNotificationSender(pushVapidPublicKey, pushVapidPrivateKey));
    builder.Services.AddHostedService<PushNotificationWorker>();
builder.Services.AddSingleton<IAnalyticsReportProvider>(services => useEventAnalytics
    ? new EventAnalyticsReportProvider(services.GetRequiredService<IAnalyticsFactStore>())
    : new SimulatedAnalyticsReportProvider());

var app = builder.Build();
if (!useSqliteCloudLedger && !string.IsNullOrWhiteSpace(postgresConnectionString))
{
    await PostgresMigrations.ApplyAsync(postgresConnectionString);
}
if (entraConfigured)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (portalAllowedOrigins.Length > 0)
{
    app.UseCors("Portal");
}

app.UseHttpsRedirection();

// Health check endpoints for Kubernetes readiness and liveness probes
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", timestamp = DateTimeOffset.UtcNow }))
    .WithName("HealthLive")
    .AllowAnonymous();

app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", timestamp = DateTimeOffset.UtcNow }))
    .WithName("HealthReady")
    .AllowAnonymous();

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

app.MapGet("/api/v1/tenants/{tenantId}/stores/{storeId}/reports/sales",
    async (string tenantId, string storeId, DateTimeOffset? from, DateTimeOffset? to, string? timezone, string? currency, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IAnalyticsReportProvider reports) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ViewReports, auditEmitter, revocations);
        if (authorization.Result is not null)
        {
            return authorization.Result;
        }

        var now = DateTimeOffset.UtcNow;
        var reportRequest = new AnalyticsReportRequest(
            tenantId,
            storeId,
            from ?? now.AddDays(-1),
            to ?? now.AddDays(1),
            string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone,
            string.IsNullOrWhiteSpace(currency) ? "USD" : currency);

        try
        {
            return Results.Ok(await reports.GetSalesReportAsync(reportRequest, request.HttpContext.RequestAborted));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    });

app.MapGet("/api/v1/tenants/{tenantId}/stores/{storeId}/sync-health",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, ISyncHealthReader healthReader) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ViewSyncHealth, auditEmitter, revocations);
        if (authorization.Result is not null)
        {
            return authorization.Result;
        }

        return Results.Ok(await healthReader.GetAsync(new TenantStoreScope(tenantId, storeId), request.HttpContext.RequestAborted));
    });

app.MapPost("/api/v1/dev/analytics/seed-sale",
    async (HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, AnalyticsEventIngestor ingestor) =>
    {
        if (!app.Environment.IsDevelopment()) return Results.NotFound();
        var input = await request.ReadFromJsonAsync<AnalyticsSeedSaleRequest>(request.HttpContext.RequestAborted);
        if (input is null || string.IsNullOrWhiteSpace(input.TenantId) || string.IsNullOrWhiteSpace(input.StoreId) || string.IsNullOrWhiteSpace(input.EventId))
        {
            return Results.BadRequest(new { Error = "TenantId, StoreId, and EventId are required." });
        }

        var authorization = await AuthorizeAsync(request, new TenantStoreScope(input.TenantId, input.StoreId), AuthorizationAction.ViewReports, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        var accepted = await ingestor.IngestAsync(new SaleCompletedEvent(
            input.EventId,
            input.SaleId,
            input.TenantId,
            input.StoreId,
            input.OccurredAt ?? DateTimeOffset.UtcNow,
            1,
            input.CorrelationId ?? input.EventId,
            "dev-seed",
            input.SaleId,
            input.SaleId,
            input.Currency,
            input.TotalMinor,
            "dev-seed-reference",
            input.InventoryMovements.Select(movement => new InventoryMovement(movement.ProductId, movement.QuantityDelta)).ToArray()), request.HttpContext.RequestAborted);
        return Results.Accepted(value: new { input.EventId, accepted, source = "dev-seed" });
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/analytics/reprocess",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, AnalyticsReplayService replay) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ReprocessAnalytics, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        var commandId = ReadHeader(request, "X-RetailPulse-Command-Id");
        if (string.IsNullOrWhiteSpace(commandId)) return Results.BadRequest(new { Error = "X-RetailPulse-Command-Id is required." });
        var input = await request.ReadFromJsonAsync<AnalyticsReplayRequest>(request.HttpContext.RequestAborted);
        if (input is null || string.IsNullOrWhiteSpace(input.EventId) || string.IsNullOrWhiteSpace(input.SaleId) || input.Currency.Length != 3)
        {
            return Results.BadRequest(new { Error = "EventId, SaleId, and a three-letter Currency are required." });
        }

        var result = await replay.ReplayAsync(commandId, new SaleCompletedEvent(
            input.EventId,
            input.SaleId,
            tenantId,
            storeId,
            input.OccurredAt,
            1,
            CorrelationId(request),
            "analytics-replay",
            input.SaleId,
            input.SaleId,
            input.Currency,
            input.TotalMinor,
            "replay-reference",
            input.InventoryMovements.Select(movement => new InventoryMovement(movement.ProductId, movement.QuantityDelta)).ToArray()), request.HttpContext.RequestAborted);
        return Results.Ok(result);
    });

app.MapGet("/api/v1/tenants/{tenantId}/stores/{storeId}/alerts",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IAlertsReader alerts) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ViewSyncHealth, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        return Results.Ok(await alerts.GetAlertsAsync(new TenantStoreScope(tenantId, storeId), request.HttpContext.RequestAborted));
    });

app.MapGet("/api/v1/tenants/{tenantId}/stores/{storeId}/settings",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IStoreSettingsRepository settings) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ConfigureStore, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        return Results.Ok(await settings.GetAsync(new TenantStoreScope(tenantId, storeId), request.HttpContext.RequestAborted));
    });

app.MapPut("/api/v1/tenants/{tenantId}/stores/{storeId}/settings",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IStoreSettingsRepository settings) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ConfigureStore, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        var input = await request.ReadFromJsonAsync<StoreSettingsRequest>(request.HttpContext.RequestAborted);
        if (input is null || string.IsNullOrWhiteSpace(input.DisplayName) || string.IsNullOrWhiteSpace(input.TimeZone) || string.IsNullOrWhiteSpace(input.Currency) || input.ExpectedVersion < 0)
        {
            return Results.BadRequest(new { Error = "DisplayName, TimeZone, Currency, and non-negative ExpectedVersion are required." });
        }
        var updated = await settings.UpdateAsync(new StoreSettings(tenantId, storeId, input.DisplayName.Trim(), input.TimeZone.Trim(), input.Currency.Trim().ToUpperInvariant(), input.InventoryAdjustmentsEnabled, input.ExpectedVersion), input.ExpectedVersion, request.HttpContext.RequestAborted);
        return updated is null ? Results.Conflict(new { Error = "Store settings changed since they were loaded." }) : Results.Ok(updated);
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/insights",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IInsightsService insights) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ViewInsights, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        var input = await request.ReadFromJsonAsync<InsightRequestBody>(request.HttpContext.RequestAborted);
        if (input is null || string.IsNullOrWhiteSpace(input.InsightType) || string.IsNullOrWhiteSpace(input.RequestId) || string.IsNullOrWhiteSpace(input.SourceVersion))
        {
            return Results.BadRequest(new { Error = "InsightType, RequestId, and SourceVersion are required." });
        }
        try
        {
            return Results.Ok(await insights.RequestAsync(new InsightRequest(tenantId, storeId, input.InsightType, input.RequestId, input.SourceVersion), request.HttpContext.RequestAborted));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    });

app.MapGet("/api/v1/tenants/{tenantId}/stores/{storeId}/insights/{insightId}",
    async (string tenantId, string storeId, string insightId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IInsightsService insights) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ViewInsights, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        var result = await insights.GetAsync(new TenantStoreScope(tenantId, storeId), insightId, request.HttpContext.RequestAborted);
        return result is null ? Results.NotFound() : Results.Ok(result);
    });

app.MapGet("/api/v1/tenants/{tenantId}/stores/{storeId}/notification-preferences",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IAlertsReader alerts) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ManageNotificationPreferences, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        return Results.Ok(await alerts.GetPreferencesAsync(new TenantStoreScope(tenantId, storeId), authorization.Token!.SubjectId, request.HttpContext.RequestAborted));
    });

app.MapGet("/api/v1/tenants/{tenantId}/stores/{storeId}/push/public-key",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ManageNotificationPreferences, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        return string.IsNullOrWhiteSpace(pushVapidPublicKey) ? Results.NotFound() : Results.Ok(new { publicKey = pushVapidPublicKey });
    });

app.MapPut("/api/v1/tenants/{tenantId}/stores/{storeId}/push-subscription",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IPushSubscriptionStore subscriptions) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ManageNotificationPreferences, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        var input = await request.ReadFromJsonAsync<PushSubscriptionRequest>(request.HttpContext.RequestAborted);
        if (input is null || string.IsNullOrWhiteSpace(input.Endpoint) || string.IsNullOrWhiteSpace(input.P256dh) || string.IsNullOrWhiteSpace(input.Auth))
        {
            return Results.BadRequest(new { Error = "Push endpoint and encryption keys are required." });
        }
        await subscriptions.RegisterAsync(new PushSubscription(tenantId, storeId, authorization.Token!.SubjectId, input.Endpoint, input.P256dh, input.Auth, DateTimeOffset.UtcNow), request.HttpContext.RequestAborted);
        return Results.Accepted();
    });

app.MapPut("/api/v1/tenants/{tenantId}/stores/{storeId}/notification-preferences",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IAlertsReader alerts) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.ManageNotificationPreferences, auditEmitter, revocations);
        if (authorization.Result is not null) return authorization.Result;
        var input = await request.ReadFromJsonAsync<NotificationPreferencesRequest>(request.HttpContext.RequestAborted);
        if (input is null) return Results.BadRequest(new { Error = "Notification preference values are required." });
        var saved = await alerts.SetPreferencesAsync(new NotificationPreferences(tenantId, storeId, authorization.Token!.SubjectId, input.LowStockEnabled, input.SyncFailureEnabled, DateTimeOffset.UtcNow), request.HttpContext.RequestAborted);
        return Results.Ok(saved);
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/manager/inventory-adjustments",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IInventoryCommandService inventoryCommands) =>
    {
        var authorization = await AuthorizeAsync(request, new TenantStoreScope(tenantId, storeId), AuthorizationAction.AdjustInventory, auditEmitter, revocations);
        if (authorization.Result is not null)
        {
            return authorization.Result;
        }

        var token = authorization.Token!;
        if (request.ContentLength is null or 0)
        {
            return Results.BadRequest(new { Error = "An inventory adjustment command body is required." });
        }

        var command = await request.ReadFromJsonAsync<InventoryAdjustmentRequest>(request.HttpContext.RequestAborted);
        if (command is null || string.IsNullOrWhiteSpace(command.ProductId) || string.IsNullOrWhiteSpace(command.Reason) || string.IsNullOrWhiteSpace(command.CommandId) || command.QuantityDelta == 0 || command.ExpectedVersion < 0)
        {
            return Results.BadRequest(new { Error = "ProductId, non-zero QuantityDelta, Reason, CommandId, and non-negative ExpectedVersion are required." });
        }

        var result = await inventoryCommands.AdjustInventoryAsync(new InventoryAdjustmentCommand(
            tenantId,
            storeId,
            command.ProductId,
            command.QuantityDelta,
            command.Reason,
            command.CommandId,
            command.ExpectedVersion,
            token.SubjectId,
            CorrelationId(request),
            DateTimeOffset.UtcNow),
            request.HttpContext.RequestAborted);

        if (result.Outcome == ManagerCommandOutcome.Reviewable)
        {
            return Results.Conflict(new { result.Outcome, result.Error });
        }

        return Results.Ok(new
        {
            Outcome = result.Outcome.ToString(),
            Event = result.Event
        });
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/devices/register",
    async (string tenantId, string storeId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IIdentityLifecycleService lifecycle, IDomainEventPublisher events) =>
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
            await events.PublishAsync("DeviceRegistered.v1", result.RegisteredEvent!, request.HttpContext.RequestAborted);
            return Results.Ok(new { Outcome = "Accepted", Event = result.RegisteredEvent });
        }

        return Results.Ok(new { Outcome = "Duplicate", Event = result.RegisteredEvent });
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/devices/{deviceId}/revoke",
    async (string tenantId, string storeId, string deviceId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IIdentityLifecycleService lifecycle, IDomainEventPublisher events) =>
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
        if (result.Outcome == IdentityCommandOutcome.Accepted)
        {
            await events.PublishAsync("DeviceRevoked.v1", result.RevokedEvent!, request.HttpContext.RequestAborted);
        }
        return Results.Ok(new { Outcome = result.Outcome.ToString(), Event = result.RevokedEvent });
    });

app.MapPost("/api/v1/tenants/{tenantId}/stores/{storeId}/users/{subjectId}/roles",
    async (string tenantId, string storeId, string subjectId, HttpRequest request, IIdentityAuditEmitter auditEmitter, IIdentityRevocationStore revocations, IIdentityLifecycleService lifecycle, IDomainEventPublisher events) =>
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
        if (result.Outcome == IdentityCommandOutcome.Accepted)
        {
            await events.PublishAsync("UserRoleChanged.v1", result.RoleChangedEvent!, request.HttpContext.RequestAborted);
        }
        return Results.Ok(new { Outcome = result.Outcome.ToString(), Event = result.RoleChangedEvent });
    });

app.Run();

static string? ResolveStoreIdFromGroup(string? groupId)
{
    if (string.IsNullOrWhiteSpace(groupId))
    {
        return null;
    }

    var raw = Environment.GetEnvironmentVariable("RetailPulse_StoreGroupMap") ?? string.Empty;
    foreach (var segment in raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && string.Equals(parts[0], groupId, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parts[1]))
        {
            return parts[1];
        }
    }

    return null;
}

static string? ResolveTenantId(string? tenantId)
{
    if (string.IsNullOrWhiteSpace(tenantId))
    {
        return null;
    }

    var raw = Environment.GetEnvironmentVariable("RetailPulse_TenantMap") ?? string.Empty;
    foreach (var segment in raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && string.Equals(parts[0], tenantId, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parts[1]))
        {
            return parts[1];
        }
    }

    return tenantId;
}

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
    if (request.HttpContext.User.Identity?.IsAuthenticated == true && TryReadBearerToken(request.HttpContext.User, out token))
    {
        return true;
    }

    if (TryReadBearerTokenFromAuthorizationHeader(request, out token))
    {
        return true;
    }

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

static bool TryReadBearerTokenFromAuthorizationHeader(HttpRequest request, out IdentityToken token)
{
    token = default!;
    var header = ReadHeader(request, "Authorization");
    if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var jwt = header["Bearer ".Length..].Trim();
    return TryReadJwtToken(jwt, out token);
}

static bool TryReadJwtToken(string jwt, out IdentityToken token)
{
    token = default!;
    if (string.IsNullOrWhiteSpace(jwt))
    {
        return false;
    }

    var parts = jwt.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2)
    {
        return false;
    }

    try
    {
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        var padded = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var claims = document.RootElement;

        var subjectId = GetStringClaim(claims, "oid", "sub");
        var tenantId = ResolveTenantId(GetStringClaim(claims, "tid"));
        var tokenId = GetStringClaim(claims, "jti") ?? subjectId;
        var issuedAt = GetUnixClaim(claims, "iat") ?? DateTimeOffset.UtcNow;
        var expiresAt = GetUnixClaim(claims, "exp") ?? DateTimeOffset.UtcNow.AddMinutes(5);
        var roleValues = GetStringArrayClaim(claims, "roles", "role");
        var groupValues = GetStringArrayClaim(claims, "groups", "group");

        var storeId = GetStringClaim(claims, "store_id", "storeId");
        if (string.IsNullOrWhiteSpace(storeId))
        {
            foreach (var group in groupValues)
            {
                var resolvedStoreId = ResolveStoreIdFromGroup(group);
                if (!string.IsNullOrWhiteSpace(resolvedStoreId))
                {
                    storeId = resolvedStoreId;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(tokenId) || roleValues.Length == 0 ||
            !roleValues.All(value => Enum.TryParse<IdentityRole>(value, ignoreCase: true, out _)))
        {
            return false;
        }

        token = new IdentityToken(tokenId, IdentityPrincipalType.User, subjectId, tenantId, storeId, roleValues.Select(value => Enum.Parse<IdentityRole>(value, ignoreCase: true)).ToArray(), issuedAt, expiresAt);
        return true;
    }
    catch
    {
        return false;
    }
}

static bool TryReadBearerToken(System.Security.Claims.ClaimsPrincipal principal, out IdentityToken token)
{
    token = default!;
    var subjectId = FirstClaimValue(principal,
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        "sub",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
    var tenantId = ResolveTenantId(FirstClaimValue(principal,
        "tid",
        "http://schemas.microsoft.com/identity/claims/tenantid"));
    var tokenId = FirstClaimValue(principal, "jti") ?? subjectId;
    var issuedAt = ReadUnixClaim(principal, "iat") ?? DateTimeOffset.UtcNow;
    var expiresAt = ReadUnixClaim(principal, "exp") ?? DateTimeOffset.UtcNow.AddMinutes(5);
    var roleValues = principal.Claims
        .Where(claim => claim.Type is "roles" or "role" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" or System.Security.Claims.ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var groupValues = principal.Claims
        .Where(claim => claim.Type is "groups" or "group" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups")
        .Select(claim => claim.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var storeId = FirstClaimValue(principal, "store_id", "storeId")
        ?? principal.Claims.FirstOrDefault(claim => claim.Type.EndsWith("store_id", StringComparison.OrdinalIgnoreCase) || claim.Type.EndsWith("storeId", StringComparison.OrdinalIgnoreCase))?.Value;
    if (string.IsNullOrWhiteSpace(storeId))
    {
        foreach (var group in groupValues)
        {
            var resolvedStoreId = ResolveStoreIdFromGroup(group);
            if (!string.IsNullOrWhiteSpace(resolvedStoreId))
            {
                storeId = resolvedStoreId;
                break;
            }
        }
    }

    if (string.IsNullOrWhiteSpace(subjectId) || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(tokenId) || roleValues.Length == 0 ||
        !roleValues.All(value => Enum.TryParse<IdentityRole>(value, ignoreCase: true, out _)))
    {
        return false;
    }

    var roles = roleValues.Select(value => Enum.Parse<IdentityRole>(value, ignoreCase: true)).ToArray();
    token = new IdentityToken(tokenId, IdentityPrincipalType.User, subjectId, tenantId, storeId, roles, issuedAt, expiresAt);
    return true;
}

static string? FirstClaimValue(System.Security.Claims.ClaimsPrincipal principal, params string[] claimTypes)
{
    foreach (var claimType in claimTypes)
    {
        var value = principal.FindFirst(claimType)?.Value;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    foreach (var claim in principal.Claims)
    {
        if (claimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
        {
            return claim.Value;
        }
    }

    return null;
}

static DateTimeOffset? ReadUnixClaim(System.Security.Claims.ClaimsPrincipal principal, params string[] claimTypes)
{
    foreach (var claimType in claimTypes)
    {
        var value = principal.FindFirst(claimType)?.Value;
        if (long.TryParse(value, out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
    }

    return null;
}

static string? GetStringClaim(System.Text.Json.JsonElement claims, params string[] names)
{
    foreach (var name in names)
    {
        if (claims.TryGetProperty(name, out var value) && value.ValueKind is not System.Text.Json.JsonValueKind.Null && value.ValueKind is not System.Text.Json.JsonValueKind.Undefined)
        {
            return value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : value.ToString();
        }
    }

    return null;
}

static string[] GetStringArrayClaim(System.Text.Json.JsonElement claims, params string[] names)
{
    foreach (var name in names)
    {
        if (!claims.TryGetProperty(name, out var value))
        {
            continue;
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var result = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    result.Add(item.GetString()!);
                }
            }

            if (result.Count > 0)
            {
                return result.ToArray();
            }
        }

        if (value.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }
        }
    }

    return [];
}

static DateTimeOffset? GetUnixClaim(System.Text.Json.JsonElement claims, params string[] names)
{
    foreach (var name in names)
    {
        if (claims.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
    }

    return null;
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

record InventoryAdjustmentRequest(string ProductId, int QuantityDelta, string Reason, string CommandId, int ExpectedVersion);
record NotificationPreferencesRequest(bool LowStockEnabled, bool SyncFailureEnabled);
record AnalyticsSeedSaleRequest(string TenantId, string StoreId, string EventId, string SaleId, string Currency, long TotalMinor, DateTimeOffset? OccurredAt, string? CorrelationId, IReadOnlyList<AnalyticsSeedMovement> InventoryMovements);
record AnalyticsSeedMovement(string ProductId, int QuantityDelta);
record AnalyticsReplayRequest(string EventId, string SaleId, string Currency, long TotalMinor, DateTimeOffset OccurredAt, IReadOnlyList<AnalyticsSeedMovement> InventoryMovements);
record StoreSettingsRequest(string DisplayName, string TimeZone, string Currency, bool InventoryAdjustmentsEnabled, int ExpectedVersion);
record InsightRequestBody(string InsightType, string RequestId, string SourceVersion);
record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth);
