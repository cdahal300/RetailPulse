namespace RetailPulse.BuildingBlocks;

public enum IdentityPrincipalType { User, Device }
public enum IdentityRole { Cashier, Manager, Owner, Device }

public enum AuthorizationAction
{
    ReadStoreData,
    ViewReports,
    ExecuteCheckout,
    AdjustInventory,
    ConfigureStore,
    RegisterDevice,
    RevokeDevice,
    ManageRoles
}

public enum AuthorizationFailure
{
    InvalidToken,
    ExpiredToken,
    Revoked,
    TenantScopeMismatch,
    StoreScopeMismatch,
    MissingRole
}

public sealed record IdentityToken(
    string TokenId,
    IdentityPrincipalType PrincipalType,
    string SubjectId,
    string TenantId,
    string? StoreId,
    IReadOnlyCollection<IdentityRole> Roles,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record AuthorizationRequest(TenantStoreScope Scope, AuthorizationAction Action);

public sealed record AuthorizationDecision(bool Allowed, AuthorizationFailure? Failure = null, string? Error = null)
{
    public static AuthorizationDecision Permit() => new(true);
    public static AuthorizationDecision Deny(AuthorizationFailure failure, string error) => new(false, failure, error);
}

public static class IdentityAuthorizationPolicy
{
    public static AuthorizationDecision Evaluate(IdentityToken? token, AuthorizationRequest request, DateTimeOffset now)
    {
        if (token is null || string.IsNullOrWhiteSpace(token.TokenId) || string.IsNullOrWhiteSpace(token.SubjectId) ||
            string.IsNullOrWhiteSpace(token.TenantId) || token.Roles is null || token.Roles.Count == 0)
        {
            return AuthorizationDecision.Deny(AuthorizationFailure.InvalidToken, "Token claims are incomplete.");
        }

        if (now >= token.ExpiresAt)
        {
            return AuthorizationDecision.Deny(AuthorizationFailure.ExpiredToken, "Token has expired.");
        }

        if (!string.Equals(token.TenantId, request.Scope.TenantId, StringComparison.Ordinal))
        {
            return AuthorizationDecision.Deny(AuthorizationFailure.TenantScopeMismatch, "Tenant scope mismatch.");
        }

        if (token.PrincipalType == IdentityPrincipalType.Device && token.Roles.Any(role => role != IdentityRole.Device))
        {
            return AuthorizationDecision.Deny(AuthorizationFailure.InvalidToken, "Device tokens cannot contain user roles.");
        }

        if (token.PrincipalType == IdentityPrincipalType.User && token.Roles.Contains(IdentityRole.Device))
        {
            return AuthorizationDecision.Deny(AuthorizationFailure.InvalidToken, "User tokens cannot contain device roles.");
        }

        if (token.Roles.Contains(IdentityRole.Owner))
        {
            return OwnerDecision(token, request);
        }

        if (!string.Equals(token.StoreId, request.Scope.StoreId, StringComparison.Ordinal))
        {
            return AuthorizationDecision.Deny(AuthorizationFailure.StoreScopeMismatch, "Store scope mismatch.");
        }

        var required = RequiredRolesFor(request.Action);
        if (token.Roles.Any(required.Contains))
        {
            return AuthorizationDecision.Permit();
        }

        return AuthorizationDecision.Deny(AuthorizationFailure.MissingRole, "Role does not allow this action.");
    }

    private static AuthorizationDecision OwnerDecision(IdentityToken token, AuthorizationRequest request)
    {
        if (request.Action is AuthorizationAction.RegisterDevice or AuthorizationAction.RevokeDevice)
        {
            return AuthorizationDecision.Permit();
        }

        if (!string.IsNullOrWhiteSpace(token.StoreId) && !string.Equals(token.StoreId, request.Scope.StoreId, StringComparison.Ordinal))
        {
            return AuthorizationDecision.Deny(AuthorizationFailure.StoreScopeMismatch, "Owner token is restricted to a different store.");
        }

        return AuthorizationDecision.Permit();
    }

    private static HashSet<IdentityRole> RequiredRolesFor(AuthorizationAction action) => action switch
    {
        AuthorizationAction.ReadStoreData => [IdentityRole.Cashier, IdentityRole.Manager, IdentityRole.Device],
        AuthorizationAction.ViewReports => [IdentityRole.Manager],
        AuthorizationAction.ExecuteCheckout => [IdentityRole.Cashier, IdentityRole.Manager, IdentityRole.Device],
        AuthorizationAction.AdjustInventory => [IdentityRole.Manager],
        AuthorizationAction.ConfigureStore => [IdentityRole.Manager],
        AuthorizationAction.RegisterDevice => [IdentityRole.Owner],
        AuthorizationAction.RevokeDevice => [IdentityRole.Owner],
        AuthorizationAction.ManageRoles => [IdentityRole.Owner],
        _ => []
    };
}

public sealed class BoundedAuthorizationSessionCache
{
    private readonly TimeSpan maxCacheLifetime;
    private readonly Dictionary<string, CachedIdentitySession> sessions = [];
    private readonly HashSet<string> revokedSubjects = [];
    private readonly HashSet<string> revokedTokens = [];

    public BoundedAuthorizationSessionCache(TimeSpan maxCacheLifetime)
    {
        if (maxCacheLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCacheLifetime), "Maximum cache lifetime must be positive.");
        }

        this.maxCacheLifetime = maxCacheLifetime;
    }

    public void Upsert(CachedIdentitySession session)
    {
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            throw new ArgumentException("Session identifier is required.", nameof(session));
        }

        sessions[session.SessionId] = session;
    }

    public bool TryGetValid(string sessionId, DateTimeOffset now, out CachedIdentitySession session)
    {
        session = default!;
        if (!sessions.TryGetValue(sessionId, out var existing))
        {
            return false;
        }

        if (revokedSubjects.Contains(existing.Token.SubjectId) || revokedTokens.Contains(existing.Token.TokenId))
        {
            sessions.Remove(sessionId);
            return false;
        }

        var effectiveExpiry = Min(existing.Token.ExpiresAt, existing.CachedAt + maxCacheLifetime);
        if (now >= effectiveExpiry)
        {
            sessions.Remove(sessionId);
            return false;
        }

        session = existing with { EffectiveExpiry = effectiveExpiry };
        return true;
    }

    public void RevokeSubject(string subjectId)
    {
        if (!string.IsNullOrWhiteSpace(subjectId))
        {
            revokedSubjects.Add(subjectId);
        }
    }

    public void RevokeToken(string tokenId)
    {
        if (!string.IsNullOrWhiteSpace(tokenId))
        {
            revokedTokens.Add(tokenId);
        }
    }

    public int RemoveExpired(DateTimeOffset now)
    {
        var removed = 0;
        var keys = sessions.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!TryGetValid(key, now, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}

public sealed record CachedIdentitySession(string SessionId, IdentityToken Token, DateTimeOffset CachedAt, DateTimeOffset EffectiveExpiry);
