using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ModularOutbox.Core.Channels;
using ModularOutbox.Core.Models;

namespace ModularOutbox.PostgreSQL.Interceptors;

public sealed class OutboxSaveChangesInterceptor(OutboxSignalChannel signalChannel) : SaveChangesInterceptor
{
    private bool _hasOutboxMessages;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        if (eventData.Context is not null)
        {
            _hasOutboxMessages = eventData
                .Context.ChangeTracker.Entries<OutboxMessage>()
                .Any(e => e.State == EntityState.Added);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        int savedCount = await base.SavedChangesAsync(eventData, result, cancellationToken);

        if (_hasOutboxMessages)
        {
            _hasOutboxMessages = false;
            signalChannel.Signal();
        }

        return savedCount;
    }
}
