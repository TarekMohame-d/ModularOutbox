using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using ModularOutbox.PostgreSQL.Services;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModularOutbox.PostgreSQL.Tests.Integration.Fixture;

public class PostgreSqlTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("test_outbox_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();
    public NpgsqlDataSource DataSource { get; private set; } = default!;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _container.StartAsync();

        DataSource = NpgsqlDataSource.Create(ConnectionString);

        // Apply Outbox migrations using the DbUp migration host
        var options = Options.Create(
            new ModularOutboxOptions { ConnectionString = ConnectionString, Schema = "messaging" }
        );

        var migrationService = new OutboxDatabaseMigrationService(
            options,
            NullLogger<OutboxDatabaseMigrationService>.Instance
        );
        await migrationService.StartingAsync(CancellationToken.None);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class PostgreSqlTestCollection : ICollectionFixture<PostgreSqlTestFixture>
{
    public const string Name = "PostgreSQL Integration Tests";
}
