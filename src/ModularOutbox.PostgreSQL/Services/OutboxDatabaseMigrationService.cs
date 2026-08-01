using DbUp;
using DbUp.Engine.Output;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using Npgsql;

namespace ModularOutbox.PostgreSQL.Services;

internal sealed class OutboxDatabaseMigrationService(
    IOptions<ModularOutboxOptions> options,
    ILogger<OutboxDatabaseMigrationService> logger
) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("==================================================");
        logger.LogInformation("         🚀 INITIALIZING OUTBOX MIGRATIONS        ");
        logger.LogInformation("==================================================");

        try
        {
            var outboxOptions = options.Value;
            var connectionString = outboxOptions.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "💥 Critical Error: Connection string in ModularOutboxOptions is missing or empty."
                );
            }

            var schemaName = outboxOptions.Schema;

            EnsureDatabase.For.PostgresqlDatabase(connectionString);
            EnsureSchemaExists(connectionString, schemaName);

            logger.LogInformation("Processing Outbox Migration -> Target Schema: [{Schema}]", schemaName);

            // Execute scripts embedded within this NuGet package assembly
            var packageAssembly = typeof(OutboxDatabaseMigrationService).Assembly;

            var upgrader = DeployChanges
                .To.PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    packageAssembly,
                    scriptName => scriptName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                )
                .WithTransaction()
                .JournalToPostgresqlTable(schemaName, "schema_versions")
                .LogTo(new DbUpLoggerAdapter(logger))
                .WithVariables(
                    new Dictionary<string, string>
                    {
                        { "schema", schemaName },
                    }
                )
                .Build();

            if (upgrader.IsUpgradeRequired())
            {
                var result = upgrader.PerformUpgrade();

                if (!result.Successful)
                {
                    logger.LogError(
                        result.Error,
                        "❌ Migration FAILED for Outbox schema: {Schema}",
                        schemaName
                    );
                    throw new InvalidOperationException(
                        $"Outbox database migration failed for schema [{schemaName}].",
                        result.Error
                    );
                }

                logger.LogInformation("✔ Outbox schema [{Schema}] migrated successfully.", schemaName);
            }
            else
            {
                logger.LogInformation("✔ Outbox schema [{Schema}] is up to date.", schemaName);
            }

            logger.LogInformation("============================================================");
            logger.LogInformation("    🎉 OUTBOX DATABASE MIGRATIONS COMPLETED SUCCESSFULLY!  ");
            logger.LogInformation("============================================================");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "An unhandled error occurred during Outbox database migration.");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void EnsureSchemaExists(string connectionString, string schemaName)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        using var command = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\";", connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Redirects DbUp internal logging into ASP.NET Core ILogger.
    /// </summary>
    private sealed class DbUpLoggerAdapter(ILogger logger) : IUpgradeLog
    {
        public void LogInformation(string format, params object[] args) =>
            logger.LogInformation(format, args);

        public void LogError(string format, params object[] args) => logger.LogError(format, args);

        public void LogWarning(string format, params object[] args) => logger.LogWarning(format, args);

        public void LogError(Exception ex, string format, params object[] args) =>
            logger.LogError(ex, format, args);

        public void LogTrace(string format, params object[] args)
        {
            throw new NotImplementedException();
        }

        public void LogDebug(string format, params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}
