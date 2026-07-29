using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Storage;
using ModularOutbox.PostgreSQL.Persistence;

namespace ModularOutbox.PostgreSQL.Storage;

internal sealed class PostgreSqlOutboxStorage(
    MessagingDbContext dbContext,
    IOptions<ModularOutboxOptions> options
) : IOutboxStorage
{
    private readonly string _schema = options.Value.Schema;

    public async Task<IReadOnlyList<OutboxMessage>> FetchUnprocessedBatchAsync(
        int batchSize,
        CancellationToken ct = default
    )
    {
        var sql = $$"""
            SELECT *
            FROM {{_schema}}.outbox_messages
            WHERE processed_at_utc IS NULL AND NOT dead_letter
            ORDER BY occurred_at_utc
            LIMIT {0}
            FOR UPDATE SKIP LOCKED
            """;

        return await dbContext.OutboxMessages.FromSqlRaw(sql, batchSize).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return dbContext.SaveChangesAsync(ct);
    }
}
