using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Context;
using ModularOutbox.Core.DependencyInjection;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Services;
using ModularOutbox.EntityFrameworkCore.Interceptors;

namespace ModularOutbox.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Provides extension methods on <see cref="ModularOutboxBuilder"/> to integrate
/// Entity Framework Core persistence and automated transaction interception.
/// </summary>
public static class EFModularOutboxExtensions
{
    /// <summary>
    /// Registers Entity Framework Core support for ModularOutbox by adding the scoped <see cref="OutboxSaveChangesInterceptor"/>
    /// to the dependency injection container.
    /// </summary>
    /// <param name="builder">The <see cref="ModularOutboxBuilder"/> instance.</param>
    /// <returns>
    /// The updated <see cref="ModularOutboxBuilder"/> instance to allow fluent method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>How EF Core Interception Works:</b><br/>
    /// Calling <see cref="UseEntityFrameworkCore"/> registers <see cref="OutboxSaveChangesInterceptor"/> as a
    /// <see cref="ServiceLifetime.Scoped"/> service. When attached to your <see cref="DbContext"/>, the interceptor automatically
    /// intercepts <c>DbContext.SaveChangesAsync</c> calls to convert enqueued in-memory outbox events into database records,
    /// persisting them in the exact same database transaction as your domain model changes.
    /// </para>
    /// <para>
    /// <b>Required DbContext Configuration:</b><br/>
    /// Registering this extension only adds the interceptor to DI. You must also register the interceptor on your
    /// <see cref="DbContext"/> options during container configuration.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example demonstrates how to configure ModularOutbox with EF Core and attach the interceptor to your <see cref="DbContext"/>:
    /// <code>
    /// // 1. Register ModularOutbox with EF Core support
    /// services.AddModularOutbox(builder =>
    /// {
    ///     builder.ConfigureOptions(configuration.GetSection("ModularOutbox"));
    ///     builder.RegisterHandlersFromAssemblies(typeof(Program).Assembly);
    /// })
    /// .UseEntityFrameworkCore();
    ///
    /// // 2. Attach the registered OutboxSaveChangesInterceptor to your DbContext
    /// services.AddDbContext&lt;ApplicationDbContext&gt;((sp, options) =>
    /// {
    ///     options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
    ///
    ///     // Inject and add the outbox interceptor
    ///     var outboxInterceptor = sp.GetRequiredService&lt;OutboxSaveChangesInterceptor&gt;();
    ///     options.AddInterceptors(outboxInterceptor);
    /// });
    /// </code>
    /// </example>
    public static ModularOutboxBuilder UseEntityFrameworkCore(this ModularOutboxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register using a factory delegate to bypass internal constructor restrictions in DI
        builder.Services.AddScoped(sp => new OutboxSaveChangesInterceptor(
            sp.GetRequiredService<OutboxMessageContext>(),
            sp.GetRequiredService<OutboxNotification>(),
            sp.GetRequiredService<IOutboxTypeResolver>(),
            sp.GetRequiredService<IOptions<ModularOutboxOptions>>()
        ));

        return builder;
    }
}
