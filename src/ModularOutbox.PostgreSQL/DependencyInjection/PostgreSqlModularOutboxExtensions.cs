using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.DependencyInjection;
using ModularOutbox.Core.Storage;
using ModularOutbox.PostgreSQL.Interceptors;
using ModularOutbox.PostgreSQL.Persistence;
using ModularOutbox.PostgreSQL.Storage;

namespace ModularOutbox.PostgreSQL.DependencyInjection;

public static class PostgreSqlModularOutboxExtensions
{
    /// <summary>
    /// Configures the internal storage engine and DbContext for reading and processing outbox messages.
    /// </summary>
    public static ModularOutboxBuilder UsePostgreSqlStorage(
        this ModularOutboxBuilder builder,
        string connectionString)
    {
        // Register internal MessagingDbContext specifically for the Outbox worker
        builder.Services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddScoped<IOutboxStorage, PostgreSqlOutboxStorage>();
        builder.Services.AddSingleton<OutboxSaveChangesInterceptor>();

        return builder;
    }

    /// <summary>
    /// Registers a module's DbContext so it can write outbox messages during business transactions.
    /// </summary>
    public static ModularOutboxBuilder RegisterModuleDbContext<TContext>(this ModularOutboxBuilder builder)
        where TContext : DbContext
    {
        builder.Services.AddScoped<IOutboxWriter<TContext>, EfOutboxWriter<TContext>>();
        builder.Services.AddScoped<IOutboxWriter>(sp => sp.GetRequiredService<IOutboxWriter<TContext>>());

        builder.Services.AddScoped<IInboxStore<TContext>, EfInboxStore<TContext>>();
        builder.Services.AddScoped<IInboxStore>(sp => sp.GetRequiredService<IInboxStore<TContext>>());

        return builder;
    }
}
