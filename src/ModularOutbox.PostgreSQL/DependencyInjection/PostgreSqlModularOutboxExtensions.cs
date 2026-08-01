using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Core.DependencyInjection;
using ModularOutbox.Core.Storage;
using ModularOutbox.PostgreSQL.Services;
using ModularOutbox.PostgreSQL.Storage;

namespace ModularOutbox.PostgreSQL.DependencyInjection;

/// <summary>
/// Provides extension methods on <see cref="ModularOutboxBuilder"/> to configure PostgreSQL
/// as the persistence provider using high-performance Npgsql and Dapper storage engines.
/// </summary>
public static class PostgreSqlModularOutboxExtensions
{
    /// <summary>
    /// Configures ModularOutbox to use PostgreSQL for event persistence, registering Npgsql data sources,
    /// optimized Dapper storage implementations, and automated database migration services.
    /// </summary>
    /// <param name="builder">The <see cref="ModularOutboxBuilder"/> instance.</param>
    /// <param name="connectionString">
    /// The PostgreSQL database connection string used to configure connection pooling and storage engines.
    /// </param>
    /// <returns>
    /// The updated <see cref="ModularOutboxBuilder"/> instance to allow fluent method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="connectionString"/> is <see langword="null"/>, empty, or consists only of whitespace.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Infrastructure Setup Details:</b>
    /// <list type="number">
    ///   <item>
    ///     <description>
    ///       <b>Connection Configuration:</b> Assigns <paramref name="connectionString"/> to <see cref="ModularOutbox.Core.Options.ModularOutboxOptions.ConnectionString"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Npgsql Data Source:</b> Registers an <c>NpgsqlDataSource</c> via <c>AddNpgsqlDataSource</c> for efficient connection management and native PostgreSQL binary formatting support.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>High-Performance Storage:</b> Registers <see cref="PostgreSqlOutboxStorage"/> and <see cref="PostgreSqlInboxStorage"/> as <see cref="ServiceLifetime.Singleton"/> services. These engines use raw Dapper queries for maximum throughput during worker polling and cleanup operations.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Automated Migrations:</b> Enrolls <see cref="OutboxDatabaseMigrationService"/> as an <see cref="Microsoft.Extensions.Hosting.IHostedService"/> to automatically create or update outbox and inbox database tables and indexes on application startup.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example demonstrates configuring ModularOutbox with PostgreSQL persistence:
    /// <code>
    /// var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
    ///
    /// builder.Services.AddModularOutbox(outbox =>
    /// {
    ///     outbox.ConfigureOptions(builder.Configuration.GetSection("ModularOutbox"));
    ///     outbox.RegisterHandlersFromAssemblies(typeof(Program).Assembly);
    /// })
    /// .UsePostgreSql(connectionString!);
    /// </code>
    /// </example>
    public static ModularOutboxBuilder UsePostgreSql(
        this ModularOutboxBuilder builder,
        string connectionString
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Set connection string into options
        builder.ConfigureOptions(options => options.ConnectionString = connectionString);

        // Register Npgsql DataSource
        builder.Services.AddNpgsqlDataSource(connectionString);

        // Register raw Dapper/Npgsql storage engines for worker background tasks
        builder.Services.AddScoped<IOutboxStorage, PostgreSqlOutboxStorage>();
        builder.Services.AddScoped<IInboxStorage, PostgreSqlInboxStorage>();

        // Migration runner
        builder.Services.AddHostedService<OutboxDatabaseMigrationService>();

        return builder;
    }
}
