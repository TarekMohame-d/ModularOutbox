namespace ModularOutbox.Abstractions;

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }

    protected IntegrationEvent()
    {
        Id = Guid.CreateVersion7();
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    protected IntegrationEvent(Guid id, DateTimeOffset occurredAtUtc)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc;
    }
}
