using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Options;

namespace ModularOutbox.Core.DependencyInjection;

/// <summary>
/// Provides a fluent builder interface for configuring <c>ModularOutbox</c> services, assembly handler discovery,
/// and runtime configuration options.
/// </summary>
/// <remarks>
/// <para>
/// Instances of <see cref="ModularOutboxBuilder"/> are typically created during application startup when calling
/// extension methods on <see cref="IServiceCollection"/>.
/// </para>
/// <para>
/// <b>Key Capabilities:</b>
/// <list type="bullet">
///   <item><description><b>Options Aggregation:</b> Chain multiple callables to configure <see cref="ModularOutboxOptions"/> programmatically or bind them to <see cref="IConfiguration"/> sections.</description></item>
///   <item><description><b>Automatic Assembly Scanning:</b> Automatically discover and register all implementations of <see cref="IIntegrationEventHandler{TEvent}"/> with <see cref="ServiceLifetime.Scoped"/> lifetime.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="services">
/// The <see cref="IServiceCollection"/> instance to which outbox services and handlers will be registered.
/// </param>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="services"/> is <see langword="null"/>.
/// </exception>
/// <example>
/// The following example demonstrates using the builder to configure outbox options and register event handlers:
/// <code>
/// services.AddModularOutbox(builder =>
/// {
///     // Bind options from appsettings.json
///     builder.ConfigureOptions(configuration.GetSection("ModularOutbox"));
///
///     // Further customize options programmatically
///     builder.ConfigureOptions(options =>
///     {
///         options.BatchSize = 250;
///         options.EnableCleanupService = true;
///     });
///
///     // Register all handlers from the executing assembly
///     builder.RegisterHandlersFromAssemblies(Assembly.GetExecutingAssembly());
/// });
/// </code>
/// </example>
public sealed class ModularOutboxBuilder(IServiceCollection services)
{
    /// <summary>
    /// Gets the underlying <see cref="IServiceCollection"/> where outbox dependencies are registered.
    /// </summary>
    /// <value>
    /// The <see cref="IServiceCollection"/> instance provided during initialization.
    /// </value>
    public IServiceCollection Services { get; } =
        services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Gets the aggregated delegate action used to configure <see cref="ModularOutboxOptions"/>.
    /// </summary>
    internal Action<ModularOutboxOptions>? OptionsConfigurer { get; private set; }

    /// <summary>
    /// Configures outbox options programmatically via an action delegate.
    /// </summary>
    /// <param name="configure">
    /// An <see cref="Action{T}"/> delegate used to modify <see cref="ModularOutboxOptions"/> properties.
    /// </param>
    /// <returns>
    /// The current <see cref="ModularOutboxBuilder"/> instance to allow fluent method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Multiple calls to <see cref="ConfigureOptions(Action{ModularOutboxOptions})"/> will be chained and executed
    /// sequentially in the order they were registered.
    /// </remarks>
    public ModularOutboxBuilder ConfigureOptions(Action<ModularOutboxOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var existing = OptionsConfigurer;
        OptionsConfigurer = options =>
        {
            existing?.Invoke(options);
            configure(options);
        };

        return this;
    }

    /// <summary>
    /// Binds outbox options from an <see cref="IConfiguration"/> section (e.g., from <c>appsettings.json</c>).
    /// </summary>
    /// <param name="configuration">
    /// The <see cref="IConfiguration"/> section containing key-value pairs matching <see cref="ModularOutboxOptions"/> properties.
    /// </param>
    /// <returns>
    /// The current <see cref="ModularOutboxBuilder"/> instance to allow fluent method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    public ModularOutboxBuilder ConfigureOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Uses ConfigurationBinder.Bind so configuration options flow through OptionsConfigurer
        return ConfigureOptions(configuration.Bind);
    }

    /// <summary>
    /// Scans the provided assemblies for all implementations of <see cref="IIntegrationEventHandler{TEvent}"/>
    /// and registers them into the service collection with <see cref="ServiceLifetime.Scoped"/> lifetime.
    /// </summary>
    /// <param name="assemblies">
    /// An array of <see cref="Assembly"/> instances to scan for event handler implementations.
    /// </param>
    /// <returns>
    /// The current <see cref="ModularOutboxBuilder"/> instance to allow fluent method chaining.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Both public and non-public concrete classes implementing <see cref="IIntegrationEventHandler{TEvent}"/>
    /// will be registered under their implemented interface contracts.
    /// </para>
    /// <para>
    /// If <paramref name="assemblies"/> is <see langword="null"/> or empty, no scan will be performed and
    /// the method immediately returns the current builder instance.
    /// </para>
    /// </remarks>
    public ModularOutboxBuilder RegisterHandlersFromAssemblies(params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            return this;

        Services.Scan(scan =>
            scan.FromAssemblies(assemblies)
                .AddClasses(
                    classes => classes.AssignableTo(typeof(IIntegrationEventHandler<>)),
                    publicOnly: false
                )
                .AsImplementedInterfaces()
                .WithScopedLifetime()
        );

        return this;
    }
}
