using System.Security.Cryptography;
using System.Text;

namespace RetailPulse.BuildingBlocks;

public sealed record FeatureFlagContext(
    string Environment,
    string TenantId,
    string? StoreId = null,
    string? TerminalId = null,
    string? SubjectId = null,
    IReadOnlyCollection<IdentityRole>? Roles = null);

public sealed record FeatureFlagTargeting(
    HashSet<string>? Environments = null,
    HashSet<string>? StoreIds = null,
    HashSet<string>? TerminalIds = null,
    HashSet<IdentityRole>? Roles = null,
    int? Percentage = null);

public sealed record FeatureFlagDefinition(
    string Key,
    string Owner,
    string Description,
    string RiskClassification,
    bool SafeDefault,
    FeatureFlagTargeting? Targeting,
    DateTimeOffset ExpiresAt,
    long Version);

public sealed record FeatureFlagEvaluation(
    string Key,
    bool Enabled,
    long Version,
    string Reason);

public sealed record FeatureFlagDraft(
    string Key,
    string Owner,
    string Description,
    string RiskClassification,
    FeatureFlagTargeting? Targeting,
    DateTimeOffset ExpiresAt);

public sealed record FeatureFlagChangeCommand(
    string TenantId,
    string? StoreId,
    FeatureFlagDraft Draft,
    long ExpectedVersion,
    string ChangeId,
    string ActorId,
    string CorrelationId);

public sealed record FeatureFlagApprovalCommand(
    string TenantId,
    string? StoreId,
    string Key,
    long ExpectedVersion,
    string ApprovalId,
    string ApproverId,
    string CorrelationId);

public sealed record FeatureFlagRollbackCommand(
    string TenantId,
    string? StoreId,
    string Key,
    long ExpectedVersion,
    string RollbackId,
    string ActorId,
    string CorrelationId);

public sealed record FeatureFlagChangedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string? StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string FlagKey,
    long Version,
    string ChangeId,
    string ActorId,
    int SchemaVersion = 1);

public sealed record FeatureFlagApprovedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string? StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string FlagKey,
    long Version,
    string ApprovalId,
    string ApproverId,
    int SchemaVersion = 1);

public sealed record FeatureFlagSnapshotPublishedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string? StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    long Version,
    int FlagCount,
    int SchemaVersion = 1);

public sealed record FeatureFlagChangeOutcome(FeatureFlagDefinition Flag, FeatureFlagChangedV1 Event, bool Applied);
public sealed record FeatureFlagApprovalOutcome(FeatureFlagDefinition Flag, FeatureFlagApprovedV1 Event, bool Applied);
public sealed record FeatureFlagSnapshotOutcome(FeatureFlagSnapshot Snapshot, FeatureFlagSnapshotPublishedV1 Event);
public sealed record FeatureFlagAuditEntry(string TenantId, string? StoreId, string Key, long Version, string Action, string ActorId, string ReferenceId, DateTimeOffset OccurredAt);

public interface IFeatureFlagManagementService
{
    Task<FeatureFlagChangeOutcome> UpsertAsync(FeatureFlagChangeCommand command, CancellationToken cancellationToken = default);
    Task<FeatureFlagApprovalOutcome> ApproveAsync(FeatureFlagApprovalCommand command, CancellationToken cancellationToken = default);
    Task<FeatureFlagChangeOutcome> RollbackAsync(FeatureFlagRollbackCommand command, CancellationToken cancellationToken = default);
    Task<FeatureFlagEvaluation> EvaluateAsync(string tenantId, string? storeId, string key, FeatureFlagContext context, CancellationToken cancellationToken = default);
    Task<FeatureFlagSnapshotOutcome> PublishSnapshotAsync(string tenantId, string? storeId, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeatureFlagAuditEntry>> GetAuditAsync(string tenantId, string? storeId, string key, CancellationToken cancellationToken = default);
}

public sealed class InMemoryFeatureFlagManagementService(HmacFeatureFlagSnapshotSigner signer, FeatureFlagEvaluator evaluator) : IFeatureFlagManagementService
{
    private readonly Dictionary<string, ManagedFlag> flags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FeatureFlagChangeOutcome> changes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FeatureFlagApprovalOutcome> approvals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> snapshotVersions = new(StringComparer.Ordinal);
    private readonly List<FeatureFlagAuditEntry> audit = [];

    public Task<FeatureFlagChangeOutcome> UpsertAsync(FeatureFlagChangeCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(command);
        if (changes.TryGetValue(command.ChangeId, out var existingChange)) return Task.FromResult(existingChange);

        var key = ScopeKey(command.TenantId, command.StoreId, command.Draft.Key);
        var currentVersion = flags.TryGetValue(key, out var existing) ? existing.Definition.Version : 0;
        if (currentVersion != command.ExpectedVersion) throw new InvalidOperationException("Flag version changed since it was loaded.");

        var flag = new FeatureFlagDefinition(command.Draft.Key, command.Draft.Owner, command.Draft.Description, command.Draft.RiskClassification, false, command.Draft.Targeting, command.Draft.ExpiresAt, currentVersion + 1);
        var now = DateTimeOffset.UtcNow;
        var changed = new FeatureFlagChangedV1(Guid.NewGuid().ToString("N"), command.Draft.Key, command.TenantId, command.StoreId, now, command.CorrelationId, command.Draft.Key, flag.Version, command.ChangeId, command.ActorId);
        var outcome = new FeatureFlagChangeOutcome(flag, changed, true);
        flags[key] = new ManagedFlag(flag, command.ActorId, false);
        changes[command.ChangeId] = outcome;
        audit.Add(new FeatureFlagAuditEntry(command.TenantId, command.StoreId, flag.Key, flag.Version, "changed", command.ActorId, command.ChangeId, now));
        return Task.FromResult(outcome);
    }

    public Task<FeatureFlagApprovalOutcome> ApproveAsync(FeatureFlagApprovalCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (approvals.TryGetValue(command.ApprovalId, out var existingApproval)) return Task.FromResult(existingApproval);
        if (string.IsNullOrWhiteSpace(command.Key) || string.IsNullOrWhiteSpace(command.ApprovalId) || string.IsNullOrWhiteSpace(command.ApproverId) || string.IsNullOrWhiteSpace(command.CorrelationId)) throw new ArgumentException("Flag key, approval ID, approver, and correlation ID are required.", nameof(command));

        var scopeKey = ScopeKey(command.TenantId, command.StoreId, command.Key);
        if (!flags.TryGetValue(scopeKey, out var flag) || flag.Definition.Version != command.ExpectedVersion) throw new InvalidOperationException("Flag version changed since it was loaded.");
        if (string.Equals(flag.ChangedBy, command.ApproverId, StringComparison.Ordinal)) throw new InvalidOperationException("A flag change requires approval by a different actor.");

        var now = DateTimeOffset.UtcNow;
        var approved = new FeatureFlagApprovedV1(Guid.NewGuid().ToString("N"), flag.Definition.Key, command.TenantId, command.StoreId, now, command.CorrelationId, flag.Definition.Key, flag.Definition.Version, command.ApprovalId, command.ApproverId);
        var outcome = new FeatureFlagApprovalOutcome(flag.Definition, approved, true);
        flags[scopeKey] = flag with { Approved = true };
        approvals[command.ApprovalId] = outcome;
        audit.Add(new FeatureFlagAuditEntry(command.TenantId, command.StoreId, flag.Definition.Key, flag.Definition.Version, "approved", command.ApproverId, command.ApprovalId, now));
        return Task.FromResult(outcome);
    }

    public Task<FeatureFlagChangeOutcome> RollbackAsync(FeatureFlagRollbackCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (changes.TryGetValue(command.RollbackId, out var existingRollback)) return Task.FromResult(existingRollback);
        if (string.IsNullOrWhiteSpace(command.TenantId) || string.IsNullOrWhiteSpace(command.Key) || string.IsNullOrWhiteSpace(command.RollbackId) || string.IsNullOrWhiteSpace(command.ActorId) || string.IsNullOrWhiteSpace(command.CorrelationId) || command.ExpectedVersion <= 0)
        {
            throw new ArgumentException("Tenant, flag key, positive expected version, rollback ID, actor, and correlation ID are required.", nameof(command));
        }

        var scopeKey = ScopeKey(command.TenantId, command.StoreId, command.Key);
        if (!flags.TryGetValue(scopeKey, out var current) || current.Definition.Version != command.ExpectedVersion) throw new InvalidOperationException("Flag version changed since it was loaded.");

        var flag = current.Definition with { Targeting = new FeatureFlagTargeting(Percentage: 0), Version = current.Definition.Version + 1 };
        var now = DateTimeOffset.UtcNow;
        var changed = new FeatureFlagChangedV1(Guid.NewGuid().ToString("N"), flag.Key, command.TenantId, command.StoreId, now, command.CorrelationId, flag.Key, flag.Version, command.RollbackId, command.ActorId);
        var outcome = new FeatureFlagChangeOutcome(flag, changed, true);
        flags[scopeKey] = new ManagedFlag(flag, command.ActorId, true);
        changes[command.RollbackId] = outcome;
        audit.Add(new FeatureFlagAuditEntry(command.TenantId, command.StoreId, flag.Key, flag.Version, "rolled-back", command.ActorId, command.RollbackId, now));
        return Task.FromResult(outcome);
    }

    public Task<FeatureFlagEvaluation> EvaluateAsync(string tenantId, string? storeId, string key, FeatureFlagContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(tenantId, context.TenantId, StringComparison.Ordinal) || !string.Equals(storeId, context.StoreId, StringComparison.Ordinal)) throw new ArgumentException("Flag context does not match the requested scope.", nameof(context));
        if (!flags.TryGetValue(ScopeKey(tenantId, storeId, key), out var flag) || !flag.Approved) return Task.FromResult(new FeatureFlagEvaluation(key, false, 0, "pending-approval"));
        return Task.FromResult(evaluator.Evaluate(flag.Definition, context, DateTimeOffset.UtcNow));
    }

    public Task<FeatureFlagSnapshotOutcome> PublishSnapshotAsync(string tenantId, string? storeId, string correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("Tenant and correlation ID are required.");
        var scopeKey = ScopeKey(tenantId, storeId, string.Empty);
        var version = snapshotVersions.TryGetValue(scopeKey, out var currentVersion) ? currentVersion + 1 : 1;
        var definitions = flags
            .Where(pair => pair.Key.StartsWith(scopeKey, StringComparison.Ordinal) && pair.Value.Approved)
            .Select(pair => pair.Value.Definition)
            .ToArray();
        var snapshot = signer.Sign(new FeatureFlagSnapshot(tenantId, storeId, version, DateTimeOffset.UtcNow.AddHours(24), definitions));
        snapshotVersions[scopeKey] = version;
        var now = DateTimeOffset.UtcNow;
        var published = new FeatureFlagSnapshotPublishedV1(Guid.NewGuid().ToString("N"), $"flags:{tenantId}:{storeId}", tenantId, storeId, now, correlationId, version, definitions.Length);
        audit.Add(new FeatureFlagAuditEntry(tenantId, storeId, "*", version, "snapshot-published", "system", published.EventId, now));
        return Task.FromResult(new FeatureFlagSnapshotOutcome(snapshot, published));
    }

    public Task<IReadOnlyList<FeatureFlagAuditEntry>> GetAuditAsync(string tenantId, string? storeId, string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<FeatureFlagAuditEntry> entries = audit.Where(entry => entry.TenantId == tenantId && entry.StoreId == storeId && entry.Key == key).ToArray();
        return Task.FromResult(entries);
    }

    private static string ScopeKey(string tenantId, string? storeId, string key) => $"{tenantId}|{storeId}|{key}";

    private static void Validate(FeatureFlagChangeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.TenantId) || string.IsNullOrWhiteSpace(command.ChangeId) || string.IsNullOrWhiteSpace(command.ActorId) || string.IsNullOrWhiteSpace(command.CorrelationId) || command.ExpectedVersion < 0)
        {
            throw new ArgumentException("Tenant, non-negative expected version, change ID, actor, and correlation ID are required.", nameof(command));
        }
        if (string.IsNullOrWhiteSpace(command.Draft.Key) || string.IsNullOrWhiteSpace(command.Draft.Owner) || string.IsNullOrWhiteSpace(command.Draft.Description) || string.IsNullOrWhiteSpace(command.Draft.RiskClassification) || command.Draft.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Flag key, owner, description, risk classification, and future expiry are required.", nameof(command));
        }
    }

    private sealed record ManagedFlag(FeatureFlagDefinition Definition, string ChangedBy, bool Approved);
}

public interface IFeatureFlagProvider
{
    Task<FeatureFlagEvaluation> EvaluateAsync(string key, FeatureFlagContext context, CancellationToken cancellationToken = default);
}

public sealed record FeatureFlagSnapshot(
    string TenantId,
    string? StoreId,
    long Version,
    DateTimeOffset ExpiresAt,
    IReadOnlyCollection<FeatureFlagDefinition> Flags,
    string Signature = "");

public interface IFeatureFlagSnapshotStore
{
    Task<FeatureFlagSnapshot?> GetSnapshotAsync(FeatureFlagContext context, CancellationToken cancellationToken = default);
    Task<bool> PublishSnapshotAsync(FeatureFlagSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed class HmacFeatureFlagSnapshotSigner(byte[] signingKey)
{
    public FeatureFlagSnapshot Sign(FeatureFlagSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot with { Signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Serialize(snapshot))) };
    }

    public bool Verify(FeatureFlagSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Signature)) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(snapshot.Signature),
                HMACSHA256.HashData(signingKey, Serialize(snapshot)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Serialize(FeatureFlagSnapshot snapshot) => Encoding.UTF8.GetBytes(string.Join('\n',
        snapshot.TenantId,
        snapshot.StoreId ?? string.Empty,
        snapshot.Version,
        snapshot.ExpiresAt.UtcDateTime.Ticks,
        string.Join('|', snapshot.Flags.OrderBy(flag => flag.Key, StringComparer.Ordinal).Select(Serialize))));

    private static string Serialize(FeatureFlagDefinition flag) => string.Join('~',
        flag.Key,
        flag.Owner,
        flag.Description,
        flag.RiskClassification,
        flag.SafeDefault,
        flag.ExpiresAt.UtcDateTime.Ticks,
        flag.Version,
        Values(flag.Targeting?.Environments),
        Values(flag.Targeting?.StoreIds),
        Values(flag.Targeting?.TerminalIds),
        Values(flag.Targeting?.Roles?.Select(role => role.ToString())),
        flag.Targeting?.Percentage);

    private static string Values(IEnumerable<string>? values) => values is null ? string.Empty : string.Join(',', values.OrderBy(value => value, StringComparer.Ordinal));
}

public sealed class InMemoryFeatureFlagSnapshotStore : IFeatureFlagSnapshotStore
{
    private readonly Dictionary<string, FeatureFlagSnapshot> snapshots = new(StringComparer.Ordinal);

    public Task<FeatureFlagSnapshot?> GetSnapshotAsync(FeatureFlagContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        snapshots.TryGetValue(Key(context.TenantId, context.StoreId), out var storeSnapshot);
        if (storeSnapshot is not null) return Task.FromResult<FeatureFlagSnapshot?>(storeSnapshot);
        snapshots.TryGetValue(Key(context.TenantId, null), out var tenantSnapshot);
        return Task.FromResult<FeatureFlagSnapshot?>(tenantSnapshot);
    }

    public Task<bool> PublishSnapshotAsync(FeatureFlagSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(snapshot.TenantId) || snapshot.Version <= 0 || snapshot.ExpiresAt == default)
        {
            throw new ArgumentException("Snapshot tenant, positive version, and expiry are required.", nameof(snapshot));
        }

        var key = Key(snapshot.TenantId, snapshot.StoreId);
        if (snapshots.TryGetValue(key, out var existing) && snapshot.Version <= existing.Version) return Task.FromResult(false);
        snapshots[key] = snapshot;
        return Task.FromResult(true);
    }

    private static string Key(string tenantId, string? storeId) => $"{tenantId}|{storeId}";
}

public sealed class OfflineFeatureFlagProvider(IFeatureFlagSnapshotStore snapshots, HmacFeatureFlagSnapshotSigner signer, FeatureFlagEvaluator evaluator) : IFeatureFlagProvider
{
    public async Task<FeatureFlagEvaluation> EvaluateAsync(string key, FeatureFlagContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Flag key is required.", nameof(key));

        try
        {
            var snapshot = await snapshots.GetSnapshotAsync(context, cancellationToken);
            if (snapshot is null) return Fallback(key, "snapshot-unavailable");
            if (!signer.Verify(snapshot)) return Fallback(key, "signature-invalid");
            if (!string.Equals(snapshot.TenantId, context.TenantId, StringComparison.Ordinal) ||
                (!string.IsNullOrWhiteSpace(snapshot.StoreId) && !string.Equals(snapshot.StoreId, context.StoreId, StringComparison.Ordinal)))
            {
                return Fallback(key, "scope-mismatch");
            }
            if (DateTimeOffset.UtcNow >= snapshot.ExpiresAt) return Fallback(key, "snapshot-expired");

            var definition = snapshot.Flags.SingleOrDefault(flag => string.Equals(flag.Key, key, StringComparison.Ordinal));
            return definition is null ? Fallback(key, "unknown-key") : evaluator.Evaluate(definition, context, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fallback(key, "provider-unavailable");
        }
    }

    private static FeatureFlagEvaluation Fallback(string key, string reason) => new(key, false, 0, reason);
}

public sealed class FeatureFlagEvaluator
{
    public FeatureFlagEvaluation Evaluate(FeatureFlagDefinition definition, FeatureFlagContext context, DateTimeOffset now)
    {
        Validate(definition, context);

        if (now >= definition.ExpiresAt)
        {
            return new FeatureFlagEvaluation(definition.Key, false, definition.Version, "expired");
        }

        if (definition.Targeting is null)
        {
            return new FeatureFlagEvaluation(definition.Key, definition.SafeDefault, definition.Version, "safe-default");
        }

        var targeting = definition.Targeting;
        if (!Matches(targeting.Environments, context.Environment) ||
            !Matches(targeting.StoreIds, context.StoreId) ||
            !Matches(targeting.TerminalIds, context.TerminalId) ||
            !MatchesRole(targeting.Roles, context.Roles) ||
            !MatchesPercentage(targeting.Percentage, definition.Key, context))
        {
            return new FeatureFlagEvaluation(definition.Key, definition.SafeDefault, definition.Version, "not-targeted");
        }

        return new FeatureFlagEvaluation(definition.Key, true, definition.Version, "targeted");
    }

    private static bool Matches(IReadOnlySet<string>? values, string? value) =>
        values is null || values.Count == 0 || (!string.IsNullOrWhiteSpace(value) && values.Contains(value));

    private static bool MatchesRole(IReadOnlySet<IdentityRole>? requiredRoles, IReadOnlyCollection<IdentityRole>? roles) =>
        requiredRoles is null || requiredRoles.Count == 0 || (roles is not null && roles.Any(requiredRoles.Contains));

    private static bool MatchesPercentage(int? percentage, string key, FeatureFlagContext context)
    {
        if (percentage is null || percentage == 100) return true;
        if (percentage == 0) return false;

        var cohortKey = string.Join('|', key, context.TenantId, context.StoreId, context.TerminalId, context.SubjectId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cohortKey));
        var cohort = BitConverter.ToUInt32(hash, 0) % 100;
        return cohort < percentage;
    }

    private static void Validate(FeatureFlagDefinition definition, FeatureFlagContext context)
    {
        if (string.IsNullOrWhiteSpace(definition.Key) || string.IsNullOrWhiteSpace(definition.Owner) ||
            string.IsNullOrWhiteSpace(definition.Description) || string.IsNullOrWhiteSpace(definition.RiskClassification) ||
            definition.ExpiresAt == default || definition.Version <= 0)
        {
            throw new ArgumentException("Flag key, owner, description, risk classification, expiry, and positive version are required.", nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(context.Environment) || string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new ArgumentException("Environment and tenant context are required.", nameof(context));
        }

        if (definition.Targeting?.Percentage is < 0 or > 100)
        {
            throw new ArgumentException("Targeting percentage must be between zero and 100.", nameof(definition));
        }
    }
}