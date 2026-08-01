using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.EntityFrameworkCore.Interceptors;

namespace ModularOutbox.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Provides Entity Framework Core <see cref="DbContextOptionsBuilder"/> extension methods
/// to seamlessly attach ModularOutbox interceptors to your database context configuration.
/// </summary>
public static class ModularOutboxDbContextOptionsExtensions
{
    /// <summary>
    /// Resolves the registered <see cref="OutboxSaveChangesInterceptor"/> from the service provider
    /// and attaches it to the <see cref="DbContextOptionsBuilder"/>.
    /// </summary>
    /// <param name="optionsBuilder">The options builder being used to configure the <see cref="DbContext"/>.</param>
    /// <param name="serviceProvider">
    /// The <see cref="IServiceProvider"/> used to resolve the scoped <see cref="OutboxSaveChangesInterceptor"/> instance.
    /// </param>
    /// <returns>
    /// The updated <see cref="DbContextOptionsBuilder"/> instance to allow fluent method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="optionsBuilder"/> or <paramref name="serviceProvider"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="OutboxSaveChangesInterceptor"/> cannot be resolved from the container.
    /// Ensure <c>UseEntityFrameworkCore()</c> was invoked on the <c>ModularOutboxBuilder</c> during outbox setup.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This extension method simplifies attaching the outbox interceptor during <see cref="DbContext"/> registration.
    /// Instead of manually retrieving <see cref="OutboxSaveChangesInterceptor"/> via
    /// <c>sp.GetRequiredService&lt;OutboxSaveChangesInterceptor&gt;()</c> and calling <c>options.AddInterceptors(...)</c>,
    /// this method encapsulates the lookup into a single fluent extension call.
    /// </para>
    /// <para>
    /// <b>Prerequisites:</b><br/>
    /// Prior to calling this method, you must register EF Core outbox support by chaining <c>.UseEntityFrameworkCore()</c>
    /// onto your <c>AddModularOutbox(...)</c> startup configuration.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example demonstrates how to cleanly configure <see cref="DbContext"/> options with ModularOutbox:
    /// <code>
    /// // 1. Register ModularOutbox with EF Core support
    /// builder.Services.AddModularOutbox(outbox =>
    /// {
    ///     outbox.ConfigureOptions(builder.Configuration.GetSection("ModularOutbox"));
    ///     outbox.RegisterHandlersFromAssemblies(typeof(Program).Assembly);
    /// })
    /// .UseEntityFrameworkCore();
    ///
    /// // 2. Configure DbContext using .UseModularOutbox(sp)
    /// builder.Services.AddDbContext&lt;ApplicationDbContext&gt;((sp, options) =>
    /// {
    ///     options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    ///            .UseModularOutbox(sp);
    /// });
    /// </code>
    /// </example>
    public static DbContextOptionsBuilder UseModularOutbox(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider
    )
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var interceptor = serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>();
        return optionsBuilder.AddInterceptors(interceptor);
    }
}
