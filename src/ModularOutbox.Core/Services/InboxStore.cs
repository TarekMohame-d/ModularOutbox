using ModularOutbox.Abstractions;
using ModularOutbox.Core.Storage;

namespace ModularOutbox.Core.Services;

internal sealed class InboxStore(IInboxStorage storage) : IInboxStore
{
    public Task<bool> HasBeenProcessedAsync(
        Guid messageId,
        string consumerName,
        CancellationToken ct = default
    )
    {
        return storage.HasBeenProcessedAsync(messageId, consumerName, ct);
    }

    public Task MarkAsProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default)
    {
        return storage.SaveAsync(messageId, consumerName, ct);
    }
}
