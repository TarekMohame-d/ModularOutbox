using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Abstractions;

namespace ModularOutbox.Core.Dispatchers;

internal sealed class IntegrationEventDispatcher(IServiceProvider serviceProvider)
    : IIntegrationEventDispatcher
{
    private static readonly ConcurrentDictionary<
        Type,
        Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>
    > InvokerCache = new();

    public Task DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        Type runtimeType = @event.GetType();

        var invoker = InvokerCache.GetOrAdd(runtimeType, CreateDispatchDelegate);

        return invoker(serviceProvider, @event, cancellationToken);
    }

    private static Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task> CreateDispatchDelegate(
        Type eventType
    )
    {
        Type handlerInterfaceType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

        return async (sp, evt, ct) =>
        {
            await using AsyncServiceScope scope = sp.CreateAsyncScope();

            // Resolve handlers from the scoped provider
            IEnumerable<object?> handlers = scope.ServiceProvider.GetServices(handlerInterfaceType);

            foreach (object? handler in handlers)
            {
                if (handler is null)
                    continue;

                var method = handlerInterfaceType.GetMethod(nameof(IIntegrationEventHandler<>.HandleAsync))!;
                var task = (Task)method.Invoke(handler, [evt, ct])!;

                await task;
            }
        };
    }
}
