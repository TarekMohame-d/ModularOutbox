using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Decorators;
using ModularOutbox.Core.Options;

namespace ModularOutbox.Core.DependencyInjection;

public sealed class ModularOutboxBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    public ModularOutboxBuilder ConfigureOptions(Action<ModularOutboxOptions> configure)
    {
        Services
            .AddOptions<ModularOutboxOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return this;
    }

    public ModularOutboxBuilder RegisterHandlersFromAssemblies(params Assembly[] assemblies)
    {
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

    public ModularOutboxBuilder EnableResilienceDecorator()
    {
        Services.Decorate(typeof(IIntegrationEventHandler<>), typeof(ResilientIntegrationEventHandler<>));
        return this;
    }
}
