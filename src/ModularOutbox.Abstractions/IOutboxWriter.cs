namespace ModularOutbox.Abstractions;

public interface IOutboxWriter
{
    void Write<TEvent>(TEvent integrationEvent)
        where TEvent : class, IIntegrationEvent;
}

public interface IOutboxWriter<TContext> : IOutboxWriter
    where TContext : class;
