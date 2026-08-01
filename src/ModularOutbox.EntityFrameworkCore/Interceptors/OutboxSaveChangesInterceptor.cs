using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Context;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Services;

namespace ModularOutbox.EntityFrameworkCore.Interceptors;

internal class OutboxSaveChangesInterceptor(
    OutboxMessageContext outboxContext,
    OutboxNotification notification,
    IOutboxTypeResolver typeResolver,
    IOptions<ModularOutboxOptions> options
) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default
    )
    {
        if (eventData.Context is null || !outboxContext.StagedEvents.Any())
            return base.SavingChangesAsync(eventData, result, ct);

        // Draining sets outboxContext.HasPendingNotification = true
        foreach (var @event in outboxContext.Drain())
        {
            var outboxMessage = new OutboxMessage
            {
                MessageId = @event.Id,
                MessageType = typeResolver.GetMessageName(@event.GetType()),
                Payload = JsonSerializer.Serialize(
                    @event,
                    @event.GetType(),
                    options.Value.JsonSerializerOptions
                ),
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };

            eventData.Context.Set<OutboxMessage>().Add(outboxMessage);
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default
    )
    {
        // Only notify if new outbox messages were staged AND no explicit transaction is active
        if (outboxContext.HasPendingNotification && eventData.Context?.Database.CurrentTransaction is null)
        {
            outboxContext.HasPendingNotification = false;
            notification.NotifyNewMessage();
        }

        return base.SavedChangesAsync(eventData, result, ct);
    }

    public Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken ct = default
    )
    {
        // Only notify if new outbox messages were staged during this transaction
        if (outboxContext.HasPendingNotification)
        {
            outboxContext.HasPendingNotification = false;
            notification.NotifyNewMessage();
        }

        return Task.CompletedTask;
    }
}
