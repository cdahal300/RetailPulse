using Npgsql;
using RetailPulse.BuildingBlocks;

namespace RetailPulse.Cloud;

public sealed class PostgresAnalyticsFactStore(string? connectionString) : IAnalyticsFactStore
{
    private readonly string? connectionString = connectionString;

    public async Task<bool> AddAsync(AnalyticsSalesFact fact, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await PostgresScope.SetAsync(connection, null, fact.TenantId, fact.StoreId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO analytics_sales_facts (source_event_id, aggregate_id, tenant_id, store_id, sale_id, currency, net_sales_minor, units_sold, occurred_at, processing_version) VALUES (@event, @aggregate, @tenant, @store, @sale, @currency, @sales, @units, @occurred, @version) ON CONFLICT (source_event_id) DO NOTHING;";
        command.Parameters.AddWithValue("event", fact.SourceEventId);
        command.Parameters.AddWithValue("aggregate", fact.AggregateId);
        command.Parameters.AddWithValue("tenant", fact.TenantId);
        command.Parameters.AddWithValue("store", fact.StoreId);
        command.Parameters.AddWithValue("sale", fact.SaleId);
        command.Parameters.AddWithValue("currency", fact.Currency);
        command.Parameters.AddWithValue("sales", fact.NetSalesMinor);
        command.Parameters.AddWithValue("units", fact.UnitsSold);
        command.Parameters.AddWithValue("occurred", fact.OccurredAt);
        command.Parameters.AddWithValue("version", fact.ProcessingVersion);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> CorrectAsync(AnalyticsSalesFact fact, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await PostgresScope.SetAsync(connection, null, fact.TenantId, fact.StoreId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO analytics_sales_facts (source_event_id, aggregate_id, tenant_id, store_id, sale_id, currency, net_sales_minor, units_sold, occurred_at, processing_version) VALUES (@event, @aggregate, @tenant, @store, @sale, @currency, @sales, @units, @occurred, @version) ON CONFLICT (source_event_id) DO UPDATE SET aggregate_id = EXCLUDED.aggregate_id, tenant_id = EXCLUDED.tenant_id, store_id = EXCLUDED.store_id, sale_id = EXCLUDED.sale_id, currency = EXCLUDED.currency, net_sales_minor = EXCLUDED.net_sales_minor, units_sold = EXCLUDED.units_sold, occurred_at = EXCLUDED.occurred_at, processing_version = EXCLUDED.processing_version;";
        command.Parameters.AddWithValue("event", fact.SourceEventId);
        command.Parameters.AddWithValue("aggregate", fact.AggregateId);
        command.Parameters.AddWithValue("tenant", fact.TenantId);
        command.Parameters.AddWithValue("store", fact.StoreId);
        command.Parameters.AddWithValue("sale", fact.SaleId);
        command.Parameters.AddWithValue("currency", fact.Currency);
        command.Parameters.AddWithValue("sales", fact.NetSalesMinor);
        command.Parameters.AddWithValue("units", fact.UnitsSold);
        command.Parameters.AddWithValue("occurred", fact.OccurredAt);
        command.Parameters.AddWithValue("version", fact.ProcessingVersion);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<AnalyticsSalesFact>> GetSalesFactsAsync(AnalyticsReportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return [];
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await PostgresScope.SetAsync(connection, null, request.TenantId, request.StoreId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT source_event_id, aggregate_id, tenant_id, store_id, sale_id, currency, net_sales_minor, units_sold, occurred_at, processing_version FROM analytics_sales_facts WHERE tenant_id = @tenant AND store_id = @store AND currency = @currency AND occurred_at >= @from AND occurred_at < @to ORDER BY occurred_at;";
        command.Parameters.AddWithValue("tenant", request.TenantId);
        command.Parameters.AddWithValue("store", request.StoreId);
        command.Parameters.AddWithValue("currency", request.Currency.ToUpperInvariant());
        command.Parameters.AddWithValue("from", request.From);
        command.Parameters.AddWithValue("to", request.To);
        var facts = new List<AnalyticsSalesFact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facts.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetInt64(6), reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(8), reader.GetInt32(9)));
        }
        return facts;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}