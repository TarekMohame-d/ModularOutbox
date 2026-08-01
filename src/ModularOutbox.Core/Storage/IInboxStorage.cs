namespace ModularOutbox.Core.Storage;

internal interface IInboxStorage
{
    Task<bool> HasBeenProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default);
    Task SaveAsync(Guid messageId, string consumerName, CancellationToken ct = default);
}
