namespace ModularOutbox.Abstractions;

public interface IInboxStore
{
    Task<bool> HasBeenProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default);
    Task MarkAsProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default);
}

public interface IInboxStore<TContext> : IInboxStore
    where TContext : class
{
    void MarkAsProcessed(Guid messageId, string consumerName);
}
