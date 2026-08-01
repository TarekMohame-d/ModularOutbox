using ModularOutbox.Core.Models;

namespace ModularOutbox.Core.Storage;

internal interface IOutboxStorage
{
    Task<IReadOnlyList<OutboxMessage>> FetchUnprocessedBatchAsync(
        int batchSize,
        CancellationToken ct = default
    );
    Task MarkCompletedAsync(long id, CancellationToken ct);
    Task MarkFailedAsync(long id, string error, CancellationToken ct);
    Task CleanupOldMessagesAsync(CancellationToken ct);
}
