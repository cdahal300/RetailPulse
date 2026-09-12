using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class AnalyticsFactIngestionTests
{
    [Fact]
    public async Task Sale_events_are_ingested_once_and_scoped_to_the_report_request()
    {
        var store = new InMemoryAnalyticsFactStore();
        var ingestor = new AnalyticsEventIngestor(store);
        var sourceEvent = CreateSaleEvent("event-1", "tenant-1", "store-1");

        Assert.True(await ingestor.IngestAsync(sourceEvent));
        Assert.False(await ingestor.IngestAsync(sourceEvent));
        await ingestor.IngestAsync(CreateSaleEvent("event-2", "tenant-2", "store-1"));

        var facts = await store.GetSalesFactsAsync(new AnalyticsReportRequest(
            "tenant-1",
            "store-1",
            DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-24T00:00:00Z"),
            "UTC",
            "USD"));

        var fact = Assert.Single(facts);
        Assert.Equal(2500, fact.NetSalesMinor);
        Assert.Equal(3, fact.UnitsSold);
    }

    private static SaleCompletedEvent CreateSaleEvent(string eventId, string tenantId, string storeId) => new(
        eventId,
        "sale-aggregate",
        tenantId,
        storeId,
        DateTimeOffset.Parse("2026-08-23T14:05:00Z"),
        1,
        "correlation-1",
        "edge",
        "sale-1",
        "local-1",
        "USD",
        2500,
        "provider-reference",
        [new InventoryMovement("coffee", -2), new InventoryMovement("tea", -1)]);
}