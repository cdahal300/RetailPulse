using System.Text.Json;
using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PostgresIdentityLifecycleService(string? connectionString) : IIdentityLifecycleService
{
    private readonly string? connectionString = connectionString;

    public async Task<DeviceCommandResult> RegisterDeviceAsync(DeviceRegistrationCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new(IdentityCommandOutcome.NotFound);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = await ReadDeviceCommandAsync(connection, transaction, command.CommandId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var version = await ReadDeviceVersionAsync(connection, transaction, command.TenantId, command.StoreId, command.DeviceId, cancellationToken) + 1;
        var @event = new DeviceRegisteredV1(Guid.NewGuid().ToString("N"), command.DeviceId, command.TenantId, command.StoreId, command.OccurredAt, command.CorrelationId, command.DeviceId, command.ActorId, version);
        var result = new DeviceCommandResult(IdentityCommandOutcome.Accepted, RegisteredEvent: @event);
        await ExecuteAsync(connection, transaction, "INSERT INTO identity_devices (tenant_id, store_id, device_id, version, revoked) VALUES (@tenant, @store, @device, @version, FALSE) ON CONFLICT (tenant_id, store_id, device_id) DO UPDATE SET version = EXCLUDED.version, revoked = FALSE; INSERT INTO identity_device_commands (command_id, command_type, outcome, event_json) VALUES (@command, 'register', @outcome, @event);", sqlCommand =>
        {
            sqlCommand.Parameters.AddWithValue("tenant", command.TenantId);
            sqlCommand.Parameters.AddWithValue("store", command.StoreId);
            sqlCommand.Parameters.AddWithValue("device", command.DeviceId);
            sqlCommand.Parameters.AddWithValue("version", version);
            sqlCommand.Parameters.AddWithValue("command", command.CommandId);
            sqlCommand.Parameters.AddWithValue("outcome", result.Outcome.ToString());
            sqlCommand.Parameters.AddWithValue("event", JsonSerializer.Serialize(@event));
        }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<DeviceCommandResult> RevokeDeviceAsync(DeviceRevocationCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new(IdentityCommandOutcome.NotFound);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = await ReadDeviceCommandAsync(connection, transaction, command.CommandId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var current = await ReadDeviceAsync(connection, transaction, command.TenantId, command.StoreId, command.DeviceId, cancellationToken);
        if (current is null)
        {
            var result = new DeviceCommandResult(IdentityCommandOutcome.NotFound);
            await StoreDeviceCommandAsync(connection, transaction, command.CommandId, result, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        if (current.Value.Revoked)
        {
            var result = new DeviceCommandResult(IdentityCommandOutcome.Duplicate);
            await StoreDeviceCommandAsync(connection, transaction, command.CommandId, result, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        var version = current.Value.Version + 1;
        var @event = new DeviceRevokedV1(Guid.NewGuid().ToString("N"), command.DeviceId, command.TenantId, command.StoreId, command.OccurredAt, command.CorrelationId, command.DeviceId, command.ActorId, version);
        var accepted = new DeviceCommandResult(IdentityCommandOutcome.Accepted, RevokedEvent: @event);
        await ExecuteAsync(connection, transaction, "UPDATE identity_devices SET version = @version, revoked = TRUE WHERE tenant_id = @tenant AND store_id = @store AND device_id = @device; INSERT INTO identity_device_commands (command_id, command_type, outcome, event_json) VALUES (@command, 'revoke', @outcome, @event);", sqlCommand =>
        {
            sqlCommand.Parameters.AddWithValue("tenant", command.TenantId);
            sqlCommand.Parameters.AddWithValue("store", command.StoreId);
            sqlCommand.Parameters.AddWithValue("device", command.DeviceId);
            sqlCommand.Parameters.AddWithValue("version", version);
            sqlCommand.Parameters.AddWithValue("command", command.CommandId);
            sqlCommand.Parameters.AddWithValue("outcome", accepted.Outcome.ToString());
            sqlCommand.Parameters.AddWithValue("event", JsonSerializer.Serialize(@event));
        }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return accepted;
    }

    public async Task<UserRoleCommandResult> ChangeUserRolesAsync(UserRoleChangeCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new(IdentityCommandOutcome.NotFound);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = await ReadRoleCommandAsync(connection, transaction, command.CommandId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var version = await ReadRoleVersionAsync(connection, transaction, command.TenantId, command.StoreId, command.SubjectId, cancellationToken) + 1;
        var @event = new UserRoleChangedV1(Guid.NewGuid().ToString("N"), command.SubjectId, command.TenantId, command.StoreId, command.OccurredAt, command.CorrelationId, command.SubjectId, command.Roles, command.ActorId, version);
        var result = new UserRoleCommandResult(IdentityCommandOutcome.Accepted, @event);
        await ExecuteAsync(connection, transaction, "INSERT INTO identity_role_assignments (tenant_id, store_id, subject_id, roles_json, version) VALUES (@tenant, @store, @subject, @roles, @version) ON CONFLICT (tenant_id, store_id, subject_id) DO UPDATE SET roles_json = EXCLUDED.roles_json, version = EXCLUDED.version; INSERT INTO identity_role_commands (command_id, outcome, event_json) VALUES (@command, @outcome, @event);", sqlCommand =>
        {
            sqlCommand.Parameters.AddWithValue("tenant", command.TenantId);
            sqlCommand.Parameters.AddWithValue("store", command.StoreId ?? string.Empty);
            sqlCommand.Parameters.AddWithValue("subject", command.SubjectId);
            sqlCommand.Parameters.AddWithValue("roles", JsonSerializer.Serialize(command.Roles));
            sqlCommand.Parameters.AddWithValue("version", version);
            sqlCommand.Parameters.AddWithValue("command", command.CommandId);
            sqlCommand.Parameters.AddWithValue("outcome", result.Outcome.ToString());
            sqlCommand.Parameters.AddWithValue("event", JsonSerializer.Serialize(@event));
        }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT pg_advisory_xact_lock(482901); CREATE TABLE IF NOT EXISTS identity_devices (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, device_id TEXT NOT NULL, version INTEGER NOT NULL, revoked BOOLEAN NOT NULL, PRIMARY KEY (tenant_id, store_id, device_id)); CREATE TABLE IF NOT EXISTS identity_device_commands (command_id TEXT PRIMARY KEY, command_type TEXT NOT NULL, outcome TEXT NOT NULL, event_json TEXT NULL); CREATE TABLE IF NOT EXISTS identity_role_assignments (tenant_id TEXT NOT NULL, store_id TEXT NOT NULL, subject_id TEXT NOT NULL, roles_json TEXT NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant_id, store_id, subject_id)); CREATE TABLE IF NOT EXISTS identity_role_commands (command_id TEXT PRIMARY KEY, outcome TEXT NOT NULL, event_json TEXT NULL);";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return connection;
    }

    private static async Task<DeviceCommandResult?> ReadDeviceCommandAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string commandId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT command_type, outcome, event_json FROM identity_device_commands WHERE command_id = @command;");
        command.Parameters.AddWithValue("command", commandId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var commandType = reader.GetString(0);
        var outcome = Enum.Parse<IdentityCommandOutcome>(reader.GetString(1));
        var json = reader.IsDBNull(2) ? null : reader.GetString(2);
        return json is null ? new(outcome) : commandType == "register" ? new(outcome, JsonSerializer.Deserialize<DeviceRegisteredV1>(json)) : new(outcome, RevokedEvent: JsonSerializer.Deserialize<DeviceRevokedV1>(json));
    }

    private static async Task<UserRoleCommandResult?> ReadRoleCommandAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string commandId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT outcome, event_json FROM identity_role_commands WHERE command_id = @command;");
        command.Parameters.AddWithValue("command", commandId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var outcome = Enum.Parse<IdentityCommandOutcome>(reader.GetString(0));
        return reader.IsDBNull(1) ? new(outcome) : new(outcome, JsonSerializer.Deserialize<UserRoleChangedV1>(reader.GetString(1)));
    }

    private static async Task<int> ReadDeviceVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, string storeId, string deviceId, CancellationToken cancellationToken)
    {
        var device = await ReadDeviceAsync(connection, transaction, tenantId, storeId, deviceId, cancellationToken);
        return device?.Version ?? 0;
    }

    private static async Task<(int Version, bool Revoked)?> ReadDeviceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, string storeId, string deviceId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT version, revoked FROM identity_devices WHERE tenant_id = @tenant AND store_id = @store AND device_id = @device;");
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("store", storeId);
        command.Parameters.AddWithValue("device", deviceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetInt32(0), reader.GetBoolean(1)) : null;
    }

    private static async Task<int> ReadRoleVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, string? storeId, string subjectId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, "SELECT version FROM identity_role_assignments WHERE tenant_id = @tenant AND store_id IS NOT DISTINCT FROM @store AND subject_id = @subject;");
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("store", storeId ?? string.Empty);
        command.Parameters.AddWithValue("subject", subjectId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? 0 : Convert.ToInt32(value);
    }

    private static async Task StoreDeviceCommandAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string commandId, DeviceCommandResult result, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO identity_device_commands (command_id, command_type, outcome, event_json) VALUES (@command, 'revoke', @outcome, NULL);", command => { command.Parameters.AddWithValue("command", commandId); command.Parameters.AddWithValue("outcome", result.Outcome.ToString()); }, cancellationToken);

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Action<NpgsqlCommand> addParameters, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, sql);
        addParameters(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql) { var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; return command; }
}
