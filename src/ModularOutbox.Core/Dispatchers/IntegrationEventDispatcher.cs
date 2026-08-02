using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Abstractions;

namespace ModularOutbox.Core.Dispatchers;

public sealed class IntegrationEventDispatcher(IServiceProvider serviceProvider) : IIntegrationEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, IntegrationEventHandlerWrapper> HandlerWrappersCache =
        new();

    public Task DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        Type runtimeEventType = @event.GetType();

        IntegrationEventHandlerWrapper wrapper = HandlerWrappersCache.GetOrAdd(
            runtimeEventType,
            static type =>
                (IntegrationEventHandlerWrapper)
                    Activator.CreateInstance(
                        typeof(IntegrationEventHandlerWrapperImpl<>).MakeGenericType(type)
                    )!
        );

        return wrapper.HandleAsync(@event, serviceProvider, cancellationToken);
    }
}

internal abstract class IntegrationEventHandlerWrapper
{
    public abstract Task HandleAsync(
        IIntegrationEvent @event,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    );
}

internal sealed class IntegrationEventHandlerWrapperImpl<TEvent> : IntegrationEventHandlerWrapper
    where TEvent : class, IIntegrationEvent
{
    public override async Task HandleAsync(
        IIntegrationEvent @event,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        var handlers = serviceProvider.GetServices<IIntegrationEventHandler<TEvent>>();
        var typedEvent = (TEvent)@event;

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(typedEvent, cancellationToken);
        }
    }
}
