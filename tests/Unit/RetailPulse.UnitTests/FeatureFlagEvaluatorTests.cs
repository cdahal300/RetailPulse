using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;
using System.Text.Json;

namespace RetailPulse.UnitTests;

public class FeatureFlagEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    private readonly FeatureFlagEvaluator evaluator = new();

    [Fact]
    public void Unknown_target_returns_safe_default()
    {
        var result = evaluator.Evaluate(Definition(safeDefault: false), Context(), Now);

        Assert.False(result.Enabled);
        Assert.Equal("safe-default", result.Reason);
    }

    [Fact]
    public void Matching_store_terminal_and_role_enables_flag()
    {
        var targeting = new FeatureFlagTargeting(
            Environments: new HashSet<string>(StringComparer.Ordinal) { "staging" },
            StoreIds: new HashSet<string>(StringComparer.Ordinal) { "store-1" },
            TerminalIds: new HashSet<string>(StringComparer.Ordinal) { "terminal-1" },
            Roles: new HashSet<IdentityRole> { IdentityRole.Manager });

        var result = evaluator.Evaluate(Definition(targeting: targeting), Context(), Now);

        Assert.True(result.Enabled);
        Assert.Equal("targeted", result.Reason);
    }

    [Fact]
    public void Percentage_targeting_is_deterministic_for_the_same_context()
    {
        var definition = Definition(targeting: new FeatureFlagTargeting(Percentage: 50));

        var first = evaluator.Evaluate(definition, Context(subjectId: "cashier-1"), Now);
        var second = evaluator.Evaluate(definition, Context(subjectId: "cashier-1"), Now);

        Assert.Equal(first.Enabled, second.Enabled);
        Assert.Equal(first.Reason, second.Reason);
    }

    [Fact]
    public void Expired_flag_is_never_enabled()
    {
        var result = evaluator.Evaluate(Definition(safeDefault: true, expiresAt: Now), Context(), Now);

        Assert.False(result.Enabled);
        Assert.Equal("expired", result.Reason);
    }

    [Fact]
    public void Invalid_targeting_percentage_is_rejected()
    {
        var definition = Definition(targeting: new FeatureFlagTargeting(Percentage: 101));

        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(definition, Context(), Now));
    }

    [Fact]
    public async Task Signed_current_snapshot_evaluates_for_its_store()
    {
        var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());
        var store = new InMemoryFeatureFlagSnapshotStore();
        var snapshot = signer.Sign(new FeatureFlagSnapshot("tenant-1", "store-1", 1, Now.AddDays(1), [Definition(targeting: new FeatureFlagTargeting(Environments: new HashSet<string>(StringComparer.Ordinal) { "staging" }))]));
        await store.PublishSnapshotAsync(snapshot);
        var provider = new OfflineFeatureFlagProvider(store, signer, evaluator);

        var result = await provider.EvaluateAsync("checkout.new-flow", Context());

        Assert.True(result.Enabled);
        Assert.Equal("targeted", result.Reason);
    }

    [Fact]
    public async Task Tampered_or_stale_snapshot_fails_closed()
    {
        var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());
        var store = new InMemoryFeatureFlagSnapshotStore();
        var tampered = signer.Sign(new FeatureFlagSnapshot("tenant-1", "store-1", 1, Now.AddDays(1), [Definition()])) with { Version = 2 };
        await store.PublishSnapshotAsync(tampered);
        var provider = new OfflineFeatureFlagProvider(store, signer, evaluator);

        var result = await provider.EvaluateAsync("checkout.new-flow", Context());

        Assert.False(result.Enabled);
        Assert.Equal("signature-invalid", result.Reason);
    }

    [Fact]
    public async Task Tampered_targeting_fails_snapshot_signature_validation()
    {
        var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());
        var store = new InMemoryFeatureFlagSnapshotStore();
        var snapshot = signer.Sign(new FeatureFlagSnapshot("tenant-1", "store-1", 1, Now.AddDays(1), [Definition(targeting: new FeatureFlagTargeting(Environments: new HashSet<string>(StringComparer.Ordinal) { "staging" }))]));
        var tampered = snapshot with { Flags = [snapshot.Flags.Single() with { Targeting = new FeatureFlagTargeting(Environments: new HashSet<string>(StringComparer.Ordinal) { "production" }) }] };
        await store.PublishSnapshotAsync(tampered);
        var provider = new OfflineFeatureFlagProvider(store, signer, evaluator);

        var result = await provider.EvaluateAsync("checkout.new-flow", Context());

        Assert.False(result.Enabled);
        Assert.Equal("signature-invalid", result.Reason);
    }

    [Fact]
    public void Snapshot_signature_survives_json_transport()
    {
        var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());
        var snapshot = signer.Sign(new FeatureFlagSnapshot("tenant-1", "store-1", 1, Now.AddDays(1), [Definition(targeting: new FeatureFlagTargeting(Environments: new HashSet<string>(StringComparer.Ordinal) { "staging" }))]));
        var transported = JsonSerializer.Deserialize<FeatureFlagSnapshot>(JsonSerializer.Serialize(snapshot));

        Assert.NotNull(transported);
        Assert.True(signer.Verify(transported));
    }

    [Fact]
    public async Task Unavailable_snapshot_source_fails_closed()
    {
        var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());
        var provider = new OfflineFeatureFlagProvider(new UnavailableSnapshotStore(), signer, evaluator);

        var result = await provider.EvaluateAsync("checkout.new-flow", Context());

        Assert.False(result.Enabled);
        Assert.Equal("provider-unavailable", result.Reason);
    }

    [Fact]
    public async Task Cross_scope_snapshot_fails_closed()
    {
        var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());
        var foreignSnapshot = signer.Sign(new FeatureFlagSnapshot("tenant-2", "store-2", 1, Now.AddDays(1), [Definition()]));
        var provider = new OfflineFeatureFlagProvider(new StaticSnapshotStore(foreignSnapshot), signer, evaluator);

        var result = await provider.EvaluateAsync("checkout.new-flow", Context());

        Assert.False(result.Enabled);
        Assert.Equal("scope-mismatch", result.Reason);
    }

    [Fact]
    public async Task Older_snapshot_cannot_replace_newer_snapshot()
    {
        var store = new InMemoryFeatureFlagSnapshotStore();
        var current = new FeatureFlagSnapshot("tenant-1", "store-1", 2, Now.AddDays(1), [Definition()]);
        var stale = current with { Version = 1 };

        Assert.True(await store.PublishSnapshotAsync(current));
        Assert.False(await store.PublishSnapshotAsync(stale));
    }

    [Fact]
    public async Task Sqlite_snapshot_store_preserves_signed_snapshot_after_restart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"retailpulse-flags-{Guid.NewGuid():N}.db");
        try
        {
            var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());
            var snapshot = signer.Sign(new FeatureFlagSnapshot("tenant-1", "store-1", 1, Now.AddDays(1), [Definition(targeting: new FeatureFlagTargeting(Environments: new HashSet<string>(StringComparer.Ordinal) { "staging" }))]));
            await new SqliteFeatureFlagSnapshotStore(path).PublishSnapshotAsync(snapshot);
            var provider = new OfflineFeatureFlagProvider(new SqliteFeatureFlagSnapshotStore(path), signer, evaluator);

            var result = await provider.EvaluateAsync("checkout.new-flow", Context());

            Assert.True(result.Enabled);
        }
        finally
        {
            foreach (var file in new[] { path, $"{path}-wal", $"{path}-shm" })
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task Changed_flag_requires_a_different_actor_to_approve_before_activation()
    {
        var service = ManagementService();
        var changed = await service.UpsertAsync(ChangeCommand());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(new("tenant-1", "store-1", changed.Flag.Key, changed.Flag.Version, "approval-1", "owner-1", "corr-2")));
        var approved = await service.ApproveAsync(new("tenant-1", "store-1", changed.Flag.Key, changed.Flag.Version, "approval-2", "owner-2", "corr-3"));
        var evaluation = await service.EvaluateAsync("tenant-1", "store-1", changed.Flag.Key, Context());

        Assert.True(approved.Applied);
        Assert.True(evaluation.Enabled);
    }

    [Fact]
    public async Task Duplicate_change_is_idempotent_and_stale_version_is_rejected()
    {
        var service = ManagementService();
        var command = ChangeCommand();
        var first = await service.UpsertAsync(command);
        var duplicate = await service.UpsertAsync(command);

        Assert.Equal(first.Event.EventId, duplicate.Event.EventId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertAsync(ChangeCommand(expectedVersion: 0, changeId: "change-2")));
    }

    [Fact]
    public async Task Snapshot_contains_only_approved_flags_and_is_signed()
    {
        var service = ManagementService();
        var changed = await service.UpsertAsync(ChangeCommand());
        await service.ApproveAsync(new("tenant-1", "store-1", changed.Flag.Key, changed.Flag.Version, "approval-1", "owner-2", "corr-2"));
        var snapshot = await service.PublishSnapshotAsync("tenant-1", "store-1", "corr-3");
        var signer = new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray());

        Assert.Single(snapshot.Snapshot.Flags);
        Assert.True(signer.Verify(snapshot.Snapshot));
        Assert.Equal(1, snapshot.Event.FlagCount);
    }

    [Fact]
    public async Task Emergency_rollback_disables_an_approved_flag_without_waiting_for_approval()
    {
        var service = ManagementService();
        var changed = await service.UpsertAsync(ChangeCommand());
        await service.ApproveAsync(new("tenant-1", "store-1", changed.Flag.Key, changed.Flag.Version, "approval-1", "owner-2", "corr-2"));

        var rollback = await service.RollbackAsync(new("tenant-1", "store-1", changed.Flag.Key, changed.Flag.Version, "rollback-1", "owner-3", "corr-3"));
        var duplicate = await service.RollbackAsync(new("tenant-1", "store-1", changed.Flag.Key, changed.Flag.Version, "rollback-1", "owner-3", "corr-3"));
        var evaluation = await service.EvaluateAsync("tenant-1", "store-1", changed.Flag.Key, Context());

        Assert.Equal(2, rollback.Flag.Version);
        Assert.Equal(rollback.Event.EventId, duplicate.Event.EventId);
        Assert.False(evaluation.Enabled);
    }

    private static FeatureFlagDefinition Definition(bool safeDefault = false, FeatureFlagTargeting? targeting = null, DateTimeOffset? expiresAt = null) =>
        new("checkout.new-flow", "platform", "Controlled checkout rollout", "medium", safeDefault, targeting, expiresAt ?? Now.AddDays(30), 1);

    private static FeatureFlagContext Context(string subjectId = "manager-1") =>
        new("staging", "tenant-1", "store-1", "terminal-1", subjectId, [IdentityRole.Manager]);

    private InMemoryFeatureFlagManagementService ManagementService() => new(new HmacFeatureFlagSnapshotSigner("test-signing-key"u8.ToArray()), evaluator);

    private static FeatureFlagChangeCommand ChangeCommand(long expectedVersion = 0, string changeId = "change-1") =>
        new("tenant-1", "store-1", new FeatureFlagDraft("checkout.new-flow", "platform", "Controlled checkout rollout", "medium", new FeatureFlagTargeting(Environments: new HashSet<string>(StringComparer.Ordinal) { "staging" }), Now.AddDays(30)), expectedVersion, changeId, "owner-1", "corr-1");

    private sealed class UnavailableSnapshotStore : IFeatureFlagSnapshotStore
    {
        public Task<FeatureFlagSnapshot?> GetSnapshotAsync(FeatureFlagContext context, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unavailable");
        public Task<bool> PublishSnapshotAsync(FeatureFlagSnapshot snapshot, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unavailable");
    }

    private sealed class StaticSnapshotStore(FeatureFlagSnapshot snapshot) : IFeatureFlagSnapshotStore
    {
        public Task<FeatureFlagSnapshot?> GetSnapshotAsync(FeatureFlagContext context, CancellationToken cancellationToken = default) => Task.FromResult<FeatureFlagSnapshot?>(snapshot);
        public Task<bool> PublishSnapshotAsync(FeatureFlagSnapshot snapshot, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}