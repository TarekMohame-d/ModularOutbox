using ModularOutbox.Abstractions;
using ModularOutbox.Core.Context;

namespace ModularOutbox.Core.Services;

/// <inheritdoc />
internal class OutboxWriter(OutboxMessageContext context) : IOutboxWriter
{
    /// <inheritdoc />
    public void Enqueue(IIntegrationEvent @event, CancellationToken ct = default)
    {
        context.Stage(@event);
    }
}
