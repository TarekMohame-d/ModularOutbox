using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using ModularOutbox.PostgreSQL.Persistence;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace ModularOutbox.PostgreSQL.Tests.Fixtures;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder(image: "postgres:18-alpine")
            .WithDatabase("test_db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private Respawner _respawner = null!;

    public string ConnectionString => Container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Container.StartAsync();

        await ApplyMigrationsAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await InitializeRespawnerAsync(connection);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>().UseNpgsql(ConnectionString).Options;

        var outboxOptions = Options.Create(new ModularOutboxOptions());

        await using var context = new MessagingDbContext(options, outboxOptions);

        var script = context.Database.GenerateCreateScript();
        if (!string.IsNullOrWhiteSpace(script))
        {
            await context.Database.ExecuteSqlRawAsync(script);
        }
    }

    private async Task InitializeRespawnerAsync(DbConnection connection) =>
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                SchemasToInclude = ["messaging"],
                DbAdapter = DbAdapter.Postgres,
                WithReseed = true,
            }
        );

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}

[CollectionDefinition("PostgreSQL")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    // Collection fixture definition for xUnit v3
}
