namespace ModularOutbox.Abstractions;

public interface IIntegrationEventDispatcher
{
    Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default);
}
