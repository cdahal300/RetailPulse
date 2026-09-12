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

    [Fact]
    public async Task Event_report_provider_aggregates_ingested_facts_with_freshness_metadata()
    {
        var store = new InMemoryAnalyticsFactStore();
        var ingestor = new AnalyticsEventIngestor(store);
        await ingestor.IngestAsync(CreateSaleEvent("event-1", "tenant-1", "store-1"));
        var provider = new EventAnalyticsReportProvider(store);

        var report = await provider.GetSalesReportAsync(new AnalyticsReportRequest(
            "tenant-1",
            "store-1",
            DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-24T00:00:00Z"),
            "UTC",
            "USD"));

        Assert.Equal(2500, report.Summary.NetSalesMinor);
        Assert.Equal(1, report.Summary.OrderCount);
        Assert.Equal(3, report.Summary.UnitsSold);
        Assert.Equal("event-facts", report.Summary.Freshness.DataSource);
        Assert.Equal("complete", report.Summary.Freshness.Status);
        Assert.Single(report.HourlySales);
    }

    [Fact]
    public async Task Corrections_replace_a_fact_without_breaking_duplicate_delivery_safety()
    {
        var store = new InMemoryAnalyticsFactStore();
        var ingestor = new AnalyticsEventIngestor(store);
        var original = CreateSaleEvent("event-correction", "tenant-1", "store-1");
        Assert.True(await ingestor.IngestAsync(original));
        Assert.False(await ingestor.IngestAsync(original));

        var correction = original with { TotalMinor = 3100, InventoryMovements = [new InventoryMovement("coffee", -1)] };
        Assert.True(await ingestor.CorrectAsync(correction));

        var report = await new EventAnalyticsReportProvider(store).GetSalesReportAsync(new AnalyticsReportRequest(
            "tenant-1", "store-1", DateTimeOffset.Parse("2026-08-23T00:00:00Z"), DateTimeOffset.Parse("2026-08-24T00:00:00Z"), "UTC", "USD"));
        Assert.Equal(3100, report.Summary.NetSalesMinor);
        Assert.Equal(1, report.Summary.UnitsSold);
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