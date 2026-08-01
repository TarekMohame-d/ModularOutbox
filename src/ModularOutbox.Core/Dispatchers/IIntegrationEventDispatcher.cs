using ModularOutbox.Abstractions;

namespace ModularOutbox.Core.Dispatchers;

internal interface IIntegrationEventDispatcher
{
    Task DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
