using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class AnalyticsReportingTests
{
    [Fact]
    public async Task Simulated_sales_report_deduplicates_events_and_filters_scope()
    {
        var provider = new SimulatedAnalyticsReportProvider();

        var report = await provider.GetSalesReportAsync(new AnalyticsReportRequest(
            "tenant-1",
            "store-1",
            DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-24T00:00:00Z"),
            "America/New_York",
            "USD"));

        Assert.Equal("tenant-1", report.Summary.TenantId);
        Assert.Equal("store-1", report.Summary.StoreId);
        Assert.Equal("USD", report.Summary.Currency);
        Assert.Equal("America/New_York", report.Summary.TimeZone);
        Assert.Equal(5750, report.Summary.NetSalesMinor);
        Assert.Equal(3, report.Summary.OrderCount);
        Assert.Equal(6, report.Summary.UnitsSold);
        Assert.Equal(1916, report.Summary.AverageOrderValueMinor);
        Assert.Equal(4, report.Summary.Freshness.SourceEventCount);
        Assert.Equal(1, report.Summary.Freshness.DuplicateEventCount);
        Assert.False(report.Summary.Freshness.IsPartial);
        Assert.Equal("simulated-events", report.Summary.Freshness.DataSource);
        Assert.Equal("sales-report.v1", report.Summary.ReportSchemaVersion);

        Assert.Collection(report.HourlySales,
            hour =>
            {
                Assert.Equal(DateTimeOffset.Parse("2026-08-23T14:00:00Z"), hour.Hour);
                Assert.Equal(3200, hour.NetSalesMinor);
                Assert.Equal(2, hour.OrderCount);
                Assert.Equal(3, hour.UnitsSold);
            },
            hour =>
            {
                Assert.Equal(DateTimeOffset.Parse("2026-08-23T15:00:00Z"), hour.Hour);
                Assert.Equal(2550, hour.NetSalesMinor);
                Assert.Equal(1, hour.OrderCount);
                Assert.Equal(3, hour.UnitsSold);
            });

        Assert.Equal("sandwich", report.TopProducts[0].ProductId);
        Assert.Equal(2550, report.TopProducts[0].NetSalesMinor);
        Assert.Equal("coffee", report.TopProducts[1].ProductId);
        Assert.Equal("tea", report.TopProducts[2].ProductId);
    }

    [Fact]
    public async Task Simulated_sales_report_excludes_other_tenants_and_stores()
    {
        var provider = new SimulatedAnalyticsReportProvider();

        var report = await provider.GetSalesReportAsync(new AnalyticsReportRequest(
            "tenant-1",
            "store-2",
            DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-24T00:00:00Z"),
            "UTC",
            "USD"));

        Assert.Equal(1000, report.Summary.NetSalesMinor);
        Assert.Equal(1, report.Summary.OrderCount);
        Assert.Equal(1, report.Summary.UnitsSold);
        Assert.Single(report.TopProducts);
    }

    [Fact]
    public async Task Simulated_sales_report_rejects_invalid_range()
    {
        var provider = new SimulatedAnalyticsReportProvider();

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetSalesReportAsync(new AnalyticsReportRequest(
            "tenant-1",
            "store-1",
            DateTimeOffset.Parse("2026-08-24T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-23T00:00:00Z"),
            "UTC",
            "USD")));
    }
}