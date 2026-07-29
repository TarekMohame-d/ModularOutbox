namespace ModularOutbox.Abstractions;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTimeOffset OccurredAtUtc { get; }
}
