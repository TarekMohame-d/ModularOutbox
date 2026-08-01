using ModularOutbox.Abstractions;

namespace ModularOutbox.Core.Context;

internal class OutboxMessageContext
{
    private readonly List<IIntegrationEvent> _stagedEvents = [];

    public IReadOnlyCollection<IIntegrationEvent> StagedEvents => _stagedEvents.AsReadOnly();

    public bool HasPendingNotification { get; set; }

    public void Stage(IIntegrationEvent @event) => _stagedEvents.Add(@event);

    public IReadOnlyList<IIntegrationEvent> Drain()
    {
        var events = _stagedEvents.ToArray();
        _stagedEvents.Clear();
        if (events.Length > 0)
            HasPendingNotification = true;

        return events.AsReadOnly();
    }

    public void Clear() => _stagedEvents.Clear();
}
