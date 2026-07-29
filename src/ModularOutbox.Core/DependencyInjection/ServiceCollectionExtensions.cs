using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Channels;
using ModularOutbox.Core.Dispatchers;
using ModularOutbox.Core.Processing;

namespace ModularOutbox.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddModularOutbox(
        this IServiceCollection services,
        Action<ModularOutboxBuilder> configure)
    {
        var builder = new ModularOutboxBuilder(services);
        configure(builder);

        // Core Infrastructure Registration
        services.AddSingleton<OutboxSignalChannel>();
        services.AddTransient<IIntegrationEventDispatcher, IntegrationEventDispatcher>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddHostedService<OutboxProcessorBackgroundService>();

        return services;
    }
}
