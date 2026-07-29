using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularOutbox.Abstractions;

namespace ModularOutbox.Core.Dispatchers;

public sealed class IntegrationEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<IntegrationEventDispatcher> logger) : IIntegrationEventDispatcher
{
    public async Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        var eventType = integrationEvent.GetType();
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

        var handlers = serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler is null) continue;

            var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<>.HandleAsync));
            if (method is null)
            {
                logger.LogError("Method HandleAsync not found on handler {HandlerType}", handler.GetType().Name);
                continue;
            }

            var task = (Task)method.Invoke(handler, [integrationEvent, ct])!;
            await task;
        }
    }
}
