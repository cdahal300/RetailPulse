using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class IdentityLifecycleTests
{
    [Fact]
    public async Task Register_device_is_idempotent_for_same_command_id()
    {
        var lifecycle = new InMemoryIdentityLifecycleService();
        var command = new DeviceRegistrationCommand("tenant-1", "store-1", "device-1", "cmd-1", "owner-1", "corr-1", DateTimeOffset.UtcNow);

        var first = await lifecycle.RegisterDeviceAsync(command);
        var second = await lifecycle.RegisterDeviceAsync(command);

        Assert.Equal(IdentityCommandOutcome.Accepted, first.Outcome);
        Assert.Equal(first, second);
        Assert.Equal(first.RegisteredEvent!.EventId, second.RegisteredEvent!.EventId);
        Assert.Equal(1, first.RegisteredEvent.Version);
    }

    [Fact]
    public async Task Revoke_device_returns_not_found_when_device_not_registered()
    {
        var lifecycle = new InMemoryIdentityLifecycleService();

        var result = await lifecycle.RevokeDeviceAsync(new DeviceRevocationCommand("tenant-1", "store-1", "missing-device", "cmd-revoke", "owner-1", "corr-1", DateTimeOffset.UtcNow));

        Assert.Equal(IdentityCommandOutcome.NotFound, result.Outcome);
        Assert.Null(result.RevokedEvent);
    }

    [Fact]
    public async Task Register_then_revoke_device_emits_versioned_events()
    {
        var lifecycle = new InMemoryIdentityLifecycleService();
        await lifecycle.RegisterDeviceAsync(new DeviceRegistrationCommand("tenant-1", "store-1", "device-1", "cmd-register", "owner-1", "corr-1", DateTimeOffset.UtcNow));

        var revoked = await lifecycle.RevokeDeviceAsync(new DeviceRevocationCommand("tenant-1", "store-1", "device-1", "cmd-revoke", "owner-1", "corr-1", DateTimeOffset.UtcNow));

        Assert.Equal(IdentityCommandOutcome.Accepted, revoked.Outcome);
        Assert.NotNull(revoked.RevokedEvent);
        Assert.Equal("device-1", revoked.RevokedEvent!.DeviceId);
        Assert.Equal(2, revoked.RevokedEvent.Version);
    }

    [Fact]
    public async Task Change_user_roles_emits_incrementing_versions()
    {
        var lifecycle = new InMemoryIdentityLifecycleService();

        var first = await lifecycle.ChangeUserRolesAsync(new UserRoleChangeCommand("tenant-1", "store-1", "user-1", [IdentityRole.Cashier], "cmd-role-1", "owner-1", "corr-1", DateTimeOffset.UtcNow));
        var second = await lifecycle.ChangeUserRolesAsync(new UserRoleChangeCommand("tenant-1", "store-1", "user-1", [IdentityRole.Manager], "cmd-role-2", "owner-1", "corr-2", DateTimeOffset.UtcNow));

        Assert.Equal(IdentityCommandOutcome.Accepted, first.Outcome);
        Assert.Equal(1, first.RoleChangedEvent!.Version);
        Assert.Equal(IdentityCommandOutcome.Accepted, second.Outcome);
        Assert.Equal(2, second.RoleChangedEvent!.Version);
        Assert.Contains(IdentityRole.Manager, second.RoleChangedEvent.Roles);
    }

    [Fact]
    public void Revocation_store_blocks_subjects_and_tokens()
    {
        var revocations = new InMemoryIdentityRevocationStore();

        revocations.RevokeSubject("tenant-1", "user-1");
        revocations.RevokeToken("token-1");

        Assert.True(revocations.IsSubjectRevoked("tenant-1", "user-1"));
        Assert.False(revocations.IsSubjectRevoked("tenant-2", "user-1"));
        Assert.True(revocations.IsTokenRevoked("token-1"));
        Assert.False(revocations.IsTokenRevoked("token-2"));
    }
}
