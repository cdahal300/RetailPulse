using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PostgresIdentityAuditEmitter(string? connectionString) : IIdentityAuditEmitter
{
    private readonly string? connectionString = connectionString;

    public Task EmitPrivilegedActionAsync(PrivilegedActionAuditedV1 auditEvent, CancellationToken cancellationToken = default) =>
        WriteAsync(auditEvent, auditEvent.SubjectId, auditEvent.Action, auditEvent.Outcome, null, cancellationToken);

    public Task EmitTokenRejectedAsync(TokenRejectedAuditedV1 auditEvent, CancellationToken cancellationToken = default) =>
        WriteAsync(auditEvent, auditEvent.SubjectId, auditEvent.Action, auditEvent.Outcome, auditEvent.Failure.ToString(), cancellationToken);

    private async Task WriteAsync(IdentityAuditEvent auditEvent, string? subjectId, string action, string outcome, string? failure, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = await OpenConnectionAsync(connectionString, cancellationToken);
        await PostgresScope.SetAsync(connection, null, auditEvent.TenantId, auditEvent.StoreId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO identity_audit_events (event_id, aggregate_id, tenant_id, store_id, occurred_at, correlation_id, subject_id, action, outcome, failure, schema_version) VALUES (@event, @aggregate, @tenant, @store, @occurred, @correlation, @subject, @action, @outcome, @failure, @version) ON CONFLICT (event_id) DO NOTHING;";
        command.Parameters.AddWithValue("event", auditEvent.EventId);
        command.Parameters.AddWithValue("aggregate", auditEvent.AggregateId);
        command.Parameters.AddWithValue("tenant", auditEvent.TenantId);
        command.Parameters.AddWithValue("store", (object?)auditEvent.StoreId ?? DBNull.Value);
        command.Parameters.AddWithValue("occurred", auditEvent.OccurredAt);
        command.Parameters.AddWithValue("correlation", auditEvent.CorrelationId);
        command.Parameters.AddWithValue("subject", (object?)subjectId ?? DBNull.Value);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("outcome", outcome);
        command.Parameters.AddWithValue("failure", (object?)failure ?? DBNull.Value);
        command.Parameters.AddWithValue("version", auditEvent.SchemaVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task<NpgsqlConnection> OpenConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

public sealed class PostgresIdentityRevocationStore(string? connectionString) : IIdentityRevocationStore
{
    private readonly string? connectionString = connectionString;

    public bool IsSubjectRevoked(string tenantId, string subjectId) =>
        ExecuteBoolean("SELECT EXISTS (SELECT 1 FROM revoked_identity_subjects WHERE tenant_id = @tenant AND subject_id = @subject);", command =>
        {
            command.Parameters.AddWithValue("tenant", tenantId);
            command.Parameters.AddWithValue("subject", subjectId);
        });

    public bool IsTokenRevoked(string tokenId) =>
        ExecuteBoolean("SELECT EXISTS (SELECT 1 FROM revoked_identity_tokens WHERE token_id = @token);", command => command.Parameters.AddWithValue("token", tokenId));

    public void RevokeSubject(string tenantId, string subjectId) => Execute("INSERT INTO revoked_identity_subjects (tenant_id, subject_id, revoked_at) VALUES (@tenant, @subject, CURRENT_TIMESTAMP) ON CONFLICT (tenant_id, subject_id) DO NOTHING;", command =>
    {
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("subject", subjectId);
    });

    public void RevokeToken(string tokenId) => Execute("INSERT INTO revoked_identity_tokens (token_id, revoked_at) VALUES (@token, CURRENT_TIMESTAMP) ON CONFLICT (token_id) DO NOTHING;", command => command.Parameters.AddWithValue("token", tokenId));

    private bool ExecuteBoolean(string sql, Action<NpgsqlCommand> addParameters)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        try
        {
            using var connection = PostgresIdentityAuditEmitter.OpenConnectionAsync(connectionString, CancellationToken.None).GetAwaiter().GetResult();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            addParameters(command);
            return Convert.ToBoolean(command.ExecuteScalar());
        }
        catch (NpgsqlException)
        {
            return true;
        }
    }

    private void Execute(string sql, Action<NpgsqlCommand> addParameters)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        using var connection = PostgresIdentityAuditEmitter.OpenConnectionAsync(connectionString, CancellationToken.None).GetAwaiter().GetResult();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        addParameters(command);
        command.ExecuteNonQuery();
    }
}
