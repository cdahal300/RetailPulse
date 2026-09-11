using RetailPulse.BuildingBlocks;
using RetailPulse.Cloud;

namespace RetailPulse.IntegrationTests;

public sealed class CloudInventoryLedgerIntegrationTests
{
    [Fact]
    public async Task Sqlite_ledger_survives_reopen_and_deduplicates_command()
    {
        await using var database = TemporaryDatabase.Create();
        var movement = Movement("movement-1", "command-1", 3, 0);

        var first = await new SqliteInventoryLedger(database.Path).AppendAsync(movement);
        var reopened = new SqliteInventoryLedger(database.Path);
        var duplicate = await reopened.AppendAsync(movement);
        var balance = await reopened.GetBalanceAsync(new("tenant-1", "store-1"), "sku-1");

        Assert.Equal(InventoryAppendOutcome.Appended, first.Outcome);
        Assert.Equal(InventoryAppendOutcome.Duplicate, duplicate.Outcome);
        Assert.Equal(3, balance.Quantity);
        Assert.Equal(1, balance.Version);
    }

    [Fact]
    public async Task Sqlite_ledger_rejects_stale_version_and_negative_stock()
    {
        await using var database = TemporaryDatabase.Create();
        var ledger = new SqliteInventoryLedger(database.Path);
        await ledger.AppendAsync(Movement("movement-1", "command-1", 2, 0));

        var stale = await ledger.AppendAsync(Movement("movement-2", "command-2", 1, 0));
        var negative = await ledger.AppendAsync(Movement("movement-3", "command-3", -3, 1));

        Assert.Equal(InventoryAppendOutcome.StaleVersion, stale.Outcome);
        Assert.Equal(InventoryAppendOutcome.NegativeStock, negative.Outcome);
        Assert.Equal(2, (await ledger.GetBalanceAsync(new("tenant-1", "store-1"), "sku-1")).Quantity);
    }

    private static InventoryMovementRecord Movement(string movementId, string commandId, int quantityDelta, int expectedVersion) =>
        new("tenant-1", "store-1", movementId, "sku-1", quantityDelta, InventoryMovementReason.Adjustment, DateTimeOffset.UtcNow, expectedVersion, expectedVersion + 1, "manager-1", CatalogInventoryRole.Manager, commandId, "correlation-1");

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"retailpulse-cloud-{Guid.NewGuid():N}.db");
        public static TemporaryDatabase Create() => new();
        public ValueTask DisposeAsync()
        {
            foreach (var path in new[] { Path, $"{Path}-wal", $"{Path}-shm" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
            return ValueTask.CompletedTask;
        }
    }
}
