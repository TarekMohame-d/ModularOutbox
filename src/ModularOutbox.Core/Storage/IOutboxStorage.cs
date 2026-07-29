using ModularOutbox.Core.Models;

namespace ModularOutbox.Core.Storage;

public interface IOutboxStorage
{
    Task<IReadOnlyList<OutboxMessage>> FetchUnprocessedBatchAsync(
        int batchSize,
        CancellationToken ct = default
    );
    Task SaveChangesAsync(CancellationToken ct = default);
}
