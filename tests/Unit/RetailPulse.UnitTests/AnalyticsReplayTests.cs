using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class AnalyticsReplayTests
{
    [Fact]
    public async Task Replay_is_owner_command_idempotent_and_applies_correction_once()
    {
        var store = new InMemoryAnalyticsFactStore();
        var ingestor = new AnalyticsEventIngestor(store);
        var sourceEvent = new SaleCompletedEvent("event-1", "sale-1", "tenant-1", "store-1", DateTimeOffset.UtcNow, 1, "correlation", "test", "sale-1", "local-1", "USD", 1000, "reference", [new InventoryMovement("coffee", -1)]);
        await ingestor.IngestAsync(sourceEvent);
        var replay = new AnalyticsReplayService(ingestor);
        var corrected = sourceEvent with { TotalMinor = 1200 };

        var first = await replay.ReplayAsync("command-1", corrected);
        var second = await replay.ReplayAsync("command-1", corrected);

        Assert.True(first.Applied);
        Assert.Equal(first, second);
        var report = await new EventAnalyticsReportProvider(store).GetSalesReportAsync(new AnalyticsReportRequest("tenant-1", "store-1", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1), "UTC", "USD"));
        Assert.Equal(1200, report.Summary.NetSalesMinor);
    }
}