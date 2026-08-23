using RetailPulse.BuildingBlocks;

namespace RetailPulse.ContractTests;

public class IdentityAuthorizationContractTests
{
    [Fact]
    public void PrivilegedActionAuditedV1_contract_contains_required_metadata_and_schema_version()
    {
        var occurredAt = DateTimeOffset.Parse("2026-08-23T12:00:00Z");
        var @event = new PrivilegedActionAuditedV1(
            EventId: "evt-1",
            AggregateId: "subject-1",
            TenantId: "tenant-1",
            StoreId: "store-1",
            OccurredAt: occurredAt,
            CorrelationId: "corr-1",
            SubjectId: "user-1",
            PrincipalType: "User",
            Action: "AdjustInventory",
            Outcome: "Authorized");

        Assert.False(string.IsNullOrWhiteSpace(@event.EventId));
        Assert.False(string.IsNullOrWhiteSpace(@event.AggregateId));
        Assert.False(string.IsNullOrWhiteSpace(@event.TenantId));
        Assert.False(string.IsNullOrWhiteSpace(@event.StoreId));
        Assert.Equal(occurredAt, @event.OccurredAt);
        Assert.False(string.IsNullOrWhiteSpace(@event.CorrelationId));
        Assert.Equal(1, @event.SchemaVersion);
        Assert.Equal("user-1", @event.SubjectId);
        Assert.Equal("User", @event.PrincipalType);
        Assert.Equal("AdjustInventory", @event.Action);
        Assert.Equal("Authorized", @event.Outcome);
    }

    [Fact]
    public void TokenRejectedAuditedV1_contract_contains_required_metadata_and_failure_reason()
    {
        var occurredAt = DateTimeOffset.Parse("2026-08-23T12:05:00Z");
        var @event = new TokenRejectedAuditedV1(
            EventId: "evt-2",
            AggregateId: "subject-2",
            TenantId: "tenant-1",
            StoreId: "store-2",
            OccurredAt: occurredAt,
            CorrelationId: "corr-2",
            SubjectId: "cashier-2",
            Action: "AdjustInventory",
            Failure: AuthorizationFailure.MissingRole,
            Outcome: "Rejected");

        Assert.False(string.IsNullOrWhiteSpace(@event.EventId));
        Assert.False(string.IsNullOrWhiteSpace(@event.AggregateId));
        Assert.False(string.IsNullOrWhiteSpace(@event.TenantId));
        Assert.False(string.IsNullOrWhiteSpace(@event.StoreId));
        Assert.Equal(occurredAt, @event.OccurredAt);
        Assert.False(string.IsNullOrWhiteSpace(@event.CorrelationId));
        Assert.Equal(1, @event.SchemaVersion);
        Assert.Equal("cashier-2", @event.SubjectId);
        Assert.Equal("AdjustInventory", @event.Action);
        Assert.Equal(AuthorizationFailure.MissingRole, @event.Failure);
        Assert.Equal("Rejected", @event.Outcome);
    }
}
