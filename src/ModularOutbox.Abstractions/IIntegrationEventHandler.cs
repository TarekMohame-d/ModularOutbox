namespace ModularOutbox.Abstractions;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class, IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}
