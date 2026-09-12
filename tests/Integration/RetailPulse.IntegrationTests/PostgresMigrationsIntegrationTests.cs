using Npgsql;
using RetailPulse.Cloud;

namespace RetailPulse.IntegrationTests;

public sealed class PostgresMigrationsIntegrationTests
{
    [Fact]
    public async Task Postgres_migrations_are_repeatable_and_create_cloud_schema()
    {
        var databaseName = $"retailpulse_test_{Guid.NewGuid():N}";
        var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_TEST_HOST") ?? "127.0.0.1";
        var adminConnectionString = $"Host={postgresHost};Port=5432;Database=postgres;Username=retailpulse;Password=retailpulse-dev";
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = $"Host={postgresHost};Port=5432;Database={databaseName};Username=retailpulse;Password=retailpulse-dev";
        try
        {
            await PostgresMigrations.ApplyAsync(connectionString);
            await PostgresMigrations.ApplyAsync(connectionString);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(version) FROM schema_migrations;";
            Assert.Equal(PostgresMigrations.CurrentVersion, Convert.ToInt32(await command.ExecuteScalarAsync()));
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('inventory_movements', 'sync_delivery_status', 'identity_audit_events', 'identity_devices', 'push_subscriptions', 'store_settings');";
            Assert.Equal(6L, await command.ExecuteScalarAsync());
            command.CommandText = "SELECT COUNT(*) FROM pg_policies WHERE schemaname = 'public' AND policyname = 'tenant_store_scope';";
            Assert.Equal(11L, await command.ExecuteScalarAsync());
        }
        finally
        {
            await using var admin = new NpgsqlConnection(adminConnectionString);
            await admin.OpenAsync();
            await using var terminate = admin.CreateCommand();
            terminate.CommandText = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid();";
            terminate.Parameters.AddWithValue("database", databaseName);
            await terminate.ExecuteNonQueryAsync();
            await using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
