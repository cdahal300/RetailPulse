namespace RetailPulse.BuildingBlocks;

public sealed record DeviceRegisteredV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string DeviceId,
    string ActorId,
    int Version,
    int SchemaVersion = 1);

public sealed record DeviceRevokedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string DeviceId,
    string ActorId,
    int Version,
    int SchemaVersion = 1);

public sealed record UserRoleChangedV1(
    string EventId,
    string AggregateId,
    string TenantId,
    string? StoreId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string SubjectId,
    IReadOnlyCollection<IdentityRole> Roles,
    string ActorId,
    int Version,
    int SchemaVersion = 1);

public enum IdentityCommandOutcome { Accepted, Duplicate, NotFound }

public sealed record DeviceRegistrationCommand(string TenantId, string StoreId, string DeviceId, string CommandId, string ActorId, string CorrelationId, DateTimeOffset OccurredAt);
public sealed record DeviceRevocationCommand(string TenantId, string StoreId, string DeviceId, string CommandId, string ActorId, string CorrelationId, DateTimeOffset OccurredAt);
public sealed record UserRoleChangeCommand(string TenantId, string? StoreId, string SubjectId, IReadOnlyCollection<IdentityRole> Roles, string CommandId, string ActorId, string CorrelationId, DateTimeOffset OccurredAt);

public sealed record DeviceCommandResult(IdentityCommandOutcome Outcome, DeviceRegisteredV1? RegisteredEvent = null, DeviceRevokedV1? RevokedEvent = null)
{
    public bool IsSuccess => Outcome is IdentityCommandOutcome.Accepted or IdentityCommandOutcome.Duplicate;
}

public sealed record UserRoleCommandResult(IdentityCommandOutcome Outcome, UserRoleChangedV1? RoleChangedEvent = null)
{
    public bool IsSuccess => Outcome is IdentityCommandOutcome.Accepted or IdentityCommandOutcome.Duplicate;
}

public interface IIdentityLifecycleService
{
    Task<DeviceCommandResult> RegisterDeviceAsync(DeviceRegistrationCommand command, CancellationToken cancellationToken = default);
    Task<DeviceCommandResult> RevokeDeviceAsync(DeviceRevocationCommand command, CancellationToken cancellationToken = default);
    Task<UserRoleCommandResult> ChangeUserRolesAsync(UserRoleChangeCommand command, CancellationToken cancellationToken = default);
}

public interface IIdentityRevocationStore
{
    bool IsSubjectRevoked(string tenantId, string subjectId);
    bool IsTokenRevoked(string tokenId);
    void RevokeSubject(string tenantId, string subjectId);
    void RevokeToken(string tokenId);
}

public sealed class InMemoryIdentityRevocationStore : IIdentityRevocationStore
{
    private readonly HashSet<(string TenantId, string SubjectId)> revokedSubjects = [];
    private readonly HashSet<string> revokedTokens = [];

    public bool IsSubjectRevoked(string tenantId, string subjectId) =>
        !string.IsNullOrWhiteSpace(tenantId) &&
        !string.IsNullOrWhiteSpace(subjectId) &&
        revokedSubjects.Contains((tenantId, subjectId));

    public bool IsTokenRevoked(string tokenId) => !string.IsNullOrWhiteSpace(tokenId) && revokedTokens.Contains(tokenId);

    public void RevokeSubject(string tenantId, string subjectId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(subjectId))
        {
            revokedSubjects.Add((tenantId, subjectId));
        }
    }

    public void RevokeToken(string tokenId)
    {
        if (!string.IsNullOrWhiteSpace(tokenId))
        {
            revokedTokens.Add(tokenId);
        }
    }
}

public sealed class InMemoryIdentityLifecycleService : IIdentityLifecycleService
{
    private readonly Dictionary<string, DeviceCommandResult> deviceCommandResults = [];
    private readonly Dictionary<string, UserRoleCommandResult> roleCommandResults = [];
    private readonly Dictionary<(string TenantId, string StoreId, string DeviceId), int> deviceVersions = [];
    private readonly HashSet<(string TenantId, string StoreId, string DeviceId)> revokedDevices = [];
    private readonly Dictionary<(string TenantId, string? StoreId, string SubjectId), (IReadOnlyCollection<IdentityRole> Roles, int Version)> roleAssignments = [];

    public Task<DeviceCommandResult> RegisterDeviceAsync(DeviceRegistrationCommand command, CancellationToken cancellationToken = default)
    {
        if (deviceCommandResults.TryGetValue(command.CommandId, out var existing))
        {
            return Task.FromResult(existing);
        }

        var key = (command.TenantId, command.StoreId, command.DeviceId);
        var version = deviceVersions.GetValueOrDefault(key) + 1;
        deviceVersions[key] = version;
        revokedDevices.Remove(key);

        var @event = new DeviceRegisteredV1(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: command.DeviceId,
            TenantId: command.TenantId,
            StoreId: command.StoreId,
            OccurredAt: command.OccurredAt,
            CorrelationId: command.CorrelationId,
            DeviceId: command.DeviceId,
            ActorId: command.ActorId,
            Version: version);

        var result = new DeviceCommandResult(IdentityCommandOutcome.Accepted, RegisteredEvent: @event);
        deviceCommandResults[command.CommandId] = result;
        return Task.FromResult(result);
    }

    public Task<DeviceCommandResult> RevokeDeviceAsync(DeviceRevocationCommand command, CancellationToken cancellationToken = default)
    {
        if (deviceCommandResults.TryGetValue(command.CommandId, out var existing))
        {
            return Task.FromResult(existing);
        }

        var key = (command.TenantId, command.StoreId, command.DeviceId);
        if (!deviceVersions.TryGetValue(key, out var version))
        {
            var notFound = new DeviceCommandResult(IdentityCommandOutcome.NotFound);
            deviceCommandResults[command.CommandId] = notFound;
            return Task.FromResult(notFound);
        }

        if (revokedDevices.Contains(key))
        {
            var duplicate = new DeviceCommandResult(IdentityCommandOutcome.Duplicate);
            deviceCommandResults[command.CommandId] = duplicate;
            return Task.FromResult(duplicate);
        }

        var nextVersion = version + 1;
        deviceVersions[key] = nextVersion;
        revokedDevices.Add(key);

        var @event = new DeviceRevokedV1(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: command.DeviceId,
            TenantId: command.TenantId,
            StoreId: command.StoreId,
            OccurredAt: command.OccurredAt,
            CorrelationId: command.CorrelationId,
            DeviceId: command.DeviceId,
            ActorId: command.ActorId,
            Version: nextVersion);

        var result = new DeviceCommandResult(IdentityCommandOutcome.Accepted, RevokedEvent: @event);
        deviceCommandResults[command.CommandId] = result;
        return Task.FromResult(result);
    }

    public Task<UserRoleCommandResult> ChangeUserRolesAsync(UserRoleChangeCommand command, CancellationToken cancellationToken = default)
    {
        if (roleCommandResults.TryGetValue(command.CommandId, out var existing))
        {
            return Task.FromResult(existing);
        }

        var key = (command.TenantId, command.StoreId, command.SubjectId);
        var currentVersion = roleAssignments.TryGetValue(key, out var current) ? current.Version : 0;
        var nextVersion = currentVersion + 1;
        roleAssignments[key] = (command.Roles, nextVersion);

        var @event = new UserRoleChangedV1(
            EventId: Guid.NewGuid().ToString("N"),
            AggregateId: command.SubjectId,
            TenantId: command.TenantId,
            StoreId: command.StoreId,
            OccurredAt: command.OccurredAt,
            CorrelationId: command.CorrelationId,
            SubjectId: command.SubjectId,
            Roles: command.Roles,
            ActorId: command.ActorId,
            Version: nextVersion);

        var result = new UserRoleCommandResult(IdentityCommandOutcome.Accepted, @event);
        roleCommandResults[command.CommandId] = result;
        return Task.FromResult(result);
    }
}