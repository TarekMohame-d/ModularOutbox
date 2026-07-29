namespace ModularOutbox.Abstractions;

public interface IOutboxProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken ct = default);
}
