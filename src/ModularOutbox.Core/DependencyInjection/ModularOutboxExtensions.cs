using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Context;
using ModularOutbox.Core.Decorators;
using ModularOutbox.Core.Dispatchers;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Services;
using Polly;
using Polly.Retry;

namespace ModularOutbox.Core.DependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> and <see cref="ModularOutboxBuilder"/>
/// to register and configure ModularOutbox core infrastructure, options, and resilience policies.
/// </summary>
public static class ModularOutboxExtensions
{
    /// <summary>
    /// Registers ModularOutbox core messaging services, context management, options validation pipelines,
    /// and conditional background processing workers into the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add ModularOutbox services to.</param>
    /// <param name="configure">
    /// A delegate to configure the <see cref="ModularOutboxBuilder"/>, options, and assembly scanning parameters.
    /// </param>
    /// <returns>
    /// A <see cref="ModularOutboxBuilder"/> instance enabling fluent registration of handlers and additional features.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Registered Service Lifetimes:</b>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Service Contract</term>
    ///     <description>Lifetime / Registration Details</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="IIntegrationEventDispatcher"/></term>
    ///     <description><see cref="ServiceLifetime.Transient"/> (via <c>IntegrationEventDispatcher</c>)</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="OutboxMessageContext"/></term>
    ///     <description><see cref="ServiceLifetime.Scoped"/></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="IOutboxWriter"/></term>
    ///     <description><see cref="ServiceLifetime.Scoped"/> (via <c>OutboxWriter</c>)</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="IInboxStore"/></term>
    ///     <description><see cref="ServiceLifetime.Scoped"/> (via <c>InboxStore</c>)</description>
    ///   </item>
    ///   <item>
    ///     <term><c>OutboxNotification</c></term>
    ///     <description><see cref="ServiceLifetime.Singleton"/> (In-memory signaling primitive)</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="IOutboxTypeResolver"/></term>
    ///     <description><see cref="ServiceLifetime.Singleton"/> (via <c>OutboxTypeResolver</c>)</description>
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Options Validation &amp; Startup Guarantees:</b><br/>
    /// Options are validated on application startup via <c>ValidateDataAnnotations()</c> and <c>ValidateOnStart()</c>.
    /// If required fields (such as <see cref="ModularOutboxOptions.ConnectionString"/>) are missing or invalid,
    /// the application will fail fast at startup instead of during event delivery.
    /// </para>
    /// <para>
    /// <b>Background Workers:</b><br/>
    /// <c>OutboxDeliveryService</c> and <c>OutboxCleanupService</c> hosted services are registered conditionally based on
    /// <see cref="ModularOutboxOptions.EnableDeliveryService"/> and <see cref="ModularOutboxOptions.EnableCleanupService"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example demonstrates standard bootstrapping inside <c>Program.cs</c>:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    ///
    /// builder.Services.AddModularOutbox(outbox =>
    /// {
    ///     outbox.ConfigureOptions(builder.Configuration.GetSection("ModularOutbox"));
    ///     outbox.RegisterHandlersFromAssemblies(typeof(Program).Assembly);
    /// });
    /// </code>
    /// </example>
    public static ModularOutboxBuilder AddModularOutbox(
        this IServiceCollection services,
        Action<ModularOutboxBuilder> configure
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ModularOutboxBuilder(services);
        configure(builder);

        var optionsBuilder = services
            .AddOptions<ModularOutboxOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (builder.OptionsConfigurer is not null)
        {
            optionsBuilder.Configure(builder.OptionsConfigurer);
        }

        // Evaluate options upfront to determine conditional service registration
        var options = new ModularOutboxOptions();
        builder.OptionsConfigurer?.Invoke(options);

        // Core Messaging & Dispatcher
        services.TryAddTransient<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IInboxStore, InboxStore>();

        // Signaling & Infrastructure
        services.AddSingleton<OutboxNotification>();
        services.AddSingleton<IOutboxTypeResolver, OutboxTypeResolver>();

        // Conditional Hosted Services Registration
        if (options.EnableDeliveryService)
        {
            services.AddHostedService<OutboxDeliveryService>();
        }

        if (options.EnableCleanupService)
        {
            services.AddHostedService<OutboxCleanupService>();
        }

        return builder;
    }

    /// <summary>
    /// Registers default Polly resilience pipelines and decorates registered <see cref="IIntegrationEventHandler{TEvent}"/>
    /// instances with retry execution logic.
    /// </summary>
    /// <param name="builder">The <see cref="ModularOutboxBuilder"/> instance.</param>
    /// <returns>
    /// The updated <see cref="ModularOutboxBuilder"/> instance to allow method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Calling this method configures a named Polly resilience strategy (<c>"ModularOutbox.Default"</c>)
    /// with the following defaults:
    /// <list type="bullet">
    ///   <item><description><b>Retry Attempts:</b> Up to 3 retries (4 execution attempts total).</description></item>
    ///   <item><description><b>Backoff Strategy:</b> Exponential backoff with random jitter.</description></item>
    ///   <item><description><b>Initial Delay:</b> 200 milliseconds.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Open-generic implementations of <see cref="IIntegrationEventHandler{TEvent}"/> are decorated with
    /// <see cref="ResilientIntegrationEventHandlerDecorator{TEvent}"/> to wrap handler execution in resilience pipelines.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddModularOutbox(outbox =>
    /// {
    ///     outbox.ConfigureOptions(options => options.BatchSize = 100);
    ///     outbox.RegisterHandlersFromAssemblies(typeof(Program).Assembly);
    /// })
    /// .AddResilienceDecorators();
    /// </code>
    /// </example>
    public static ModularOutboxBuilder AddResilienceDecorators(this ModularOutboxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register default Polly resilience pipeline
        builder.Services.AddResiliencePipeline(
            "ModularOutbox.Default",
            pipeline =>
            {
                pipeline.AddRetry(
                    new RetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromMilliseconds(200),
                    }
                );
            }
        );

        builder.Services.TryDecorate(
            typeof(IIntegrationEventHandler<>),
            typeof(ResilientIntegrationEventHandlerDecorator<>)
        );

        return builder;
    }
}
