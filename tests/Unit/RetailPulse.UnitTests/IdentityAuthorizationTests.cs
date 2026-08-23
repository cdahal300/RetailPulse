using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class IdentityAuthorizationTests
{
    [Fact]
    public void Cashier_cannot_perform_manager_inventory_adjustment()
    {
        var token = UserToken([IdentityRole.Cashier], tenantId: "tenant-1", storeId: "store-1");

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new("tenant-1", "store-1"), AuthorizationAction.AdjustInventory),
            DateTimeOffset.Parse("2026-08-23T10:00:00Z"));

        Assert.False(decision.Allowed);
        Assert.Equal(AuthorizationFailure.MissingRole, decision.Failure);
    }

    [Fact]
    public void Manager_is_denied_when_store_scope_differs()
    {
        var token = UserToken([IdentityRole.Manager], tenantId: "tenant-1", storeId: "store-1");

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new("tenant-1", "store-2"), AuthorizationAction.AdjustInventory),
            DateTimeOffset.Parse("2026-08-23T10:00:00Z"));

        Assert.False(decision.Allowed);
        Assert.Equal(AuthorizationFailure.StoreScopeMismatch, decision.Failure);
    }

    [Fact]
    public void Owner_can_access_other_stores_within_same_tenant()
    {
        var token = UserToken([IdentityRole.Owner], tenantId: "tenant-1", storeId: null);

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new("tenant-1", "store-9"), AuthorizationAction.ConfigureStore),
            DateTimeOffset.Parse("2026-08-23T10:00:00Z"));

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Owner_cannot_access_other_tenant()
    {
        var token = UserToken([IdentityRole.Owner], tenantId: "tenant-1", storeId: null);

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new("tenant-2", "store-1"), AuthorizationAction.ConfigureStore),
            DateTimeOffset.Parse("2026-08-23T10:00:00Z"));

        Assert.False(decision.Allowed);
        Assert.Equal(AuthorizationFailure.TenantScopeMismatch, decision.Failure);
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var token = UserToken([IdentityRole.Manager], tenantId: "tenant-1", storeId: "store-1") with
        {
            ExpiresAt = DateTimeOffset.Parse("2026-08-23T09:59:59Z")
        };

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new("tenant-1", "store-1"), AuthorizationAction.AdjustInventory),
            DateTimeOffset.Parse("2026-08-23T10:00:00Z"));

        Assert.False(decision.Allowed);
        Assert.Equal(AuthorizationFailure.ExpiredToken, decision.Failure);
    }

    [Fact]
    public void Offline_cache_enforces_bounded_lifetime_and_revocation()
    {
        var cache = new BoundedAuthorizationSessionCache(TimeSpan.FromMinutes(5));
        var token = UserToken([IdentityRole.Manager], tenantId: "tenant-1", storeId: "store-1") with
        {
            ExpiresAt = DateTimeOffset.Parse("2026-08-23T10:30:00Z")
        };
        cache.Upsert(new CachedIdentitySession("session-1", token, DateTimeOffset.Parse("2026-08-23T10:00:00Z"), DateTimeOffset.MinValue));

        var validAtFourMinutes = cache.TryGetValid("session-1", DateTimeOffset.Parse("2026-08-23T10:04:00Z"), out var session);
        var expiresAtFiveMinutes = cache.TryGetValid("session-1", DateTimeOffset.Parse("2026-08-23T10:05:00Z"), out _);

        Assert.True(validAtFourMinutes);
        Assert.Equal(DateTimeOffset.Parse("2026-08-23T10:05:00Z"), session.EffectiveExpiry);
        Assert.False(expiresAtFiveMinutes);

        cache.Upsert(new CachedIdentitySession("session-2", token, DateTimeOffset.Parse("2026-08-23T10:06:00Z"), DateTimeOffset.MinValue));
        cache.RevokeSubject(token.SubjectId);
        Assert.False(cache.TryGetValid("session-2", DateTimeOffset.Parse("2026-08-23T10:06:01Z"), out _));
    }

    [Fact]
    public void Device_token_cannot_impersonate_user_roles()
    {
        var token = new IdentityToken(
            TokenId: "token-1",
            PrincipalType: IdentityPrincipalType.Device,
            SubjectId: "device-1",
            TenantId: "tenant-1",
            StoreId: "store-1",
            Roles: [IdentityRole.Device, IdentityRole.Manager],
            IssuedAt: DateTimeOffset.Parse("2026-08-23T09:00:00Z"),
            ExpiresAt: DateTimeOffset.Parse("2026-08-23T11:00:00Z"));

        var decision = IdentityAuthorizationPolicy.Evaluate(
            token,
            new AuthorizationRequest(new("tenant-1", "store-1"), AuthorizationAction.ExecuteCheckout),
            DateTimeOffset.Parse("2026-08-23T10:00:00Z"));

        Assert.False(decision.Allowed);
        Assert.Equal(AuthorizationFailure.InvalidToken, decision.Failure);
    }

    private static IdentityToken UserToken(IReadOnlyCollection<IdentityRole> roles, string tenantId, string? storeId) =>
        new(
            TokenId: "token-1",
            PrincipalType: IdentityPrincipalType.User,
            SubjectId: "user-1",
            TenantId: tenantId,
            StoreId: storeId,
            Roles: roles,
            IssuedAt: DateTimeOffset.Parse("2026-08-23T09:00:00Z"),
            ExpiresAt: DateTimeOffset.Parse("2026-08-23T11:00:00Z"));
}
