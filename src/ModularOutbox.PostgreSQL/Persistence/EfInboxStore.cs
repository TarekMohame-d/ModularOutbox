using Microsoft.EntityFrameworkCore;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Models;

namespace ModularOutbox.PostgreSQL.Persistence;

public sealed class EfInboxStore<TContext>(TContext dbContext) : IInboxStore<TContext>
    where TContext : DbContext
{
    public async Task<bool> HasBeenProcessedAsync(
        Guid messageId,
        string consumerName,
        CancellationToken ct = default
    )
    {
        return await dbContext
            .Set<InboxMessage>()
            .AnyAsync(x => x.Id == messageId && x.ConsumerName == consumerName, ct);
    }

    public async Task MarkAsProcessedAsync(
        Guid messageId,
        string consumerName,
        CancellationToken ct = default
    )
    {
        var inboxMessage = new InboxMessage(messageId, consumerName);
        dbContext.Set<InboxMessage>().Add(inboxMessage);
        await dbContext.SaveChangesAsync(ct);
    }

    public void MarkAsProcessed(Guid messageId, string consumerName)
    {
        var inboxMessage = new InboxMessage(messageId, consumerName);
        dbContext.Set<InboxMessage>().Add(inboxMessage);
    }
}
