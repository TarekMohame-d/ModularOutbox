using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Storage;
using Npgsql;

namespace ModularOutbox.PostgreSQL.Storage;

internal sealed class PostgreSqlOutboxStorage(
    NpgsqlDataSource dataSource,
    IOptions<ModularOutboxOptions> options,
    ILogger<PostgreSqlOutboxStorage> logger
) : IOutboxStorage
{
    private readonly ModularOutboxOptions _options = options.Value;

    public async Task<IReadOnlyList<OutboxMessage>> FetchUnprocessedBatchAsync(
        int batchSize,
        CancellationToken ct = default
    )
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var lockExpiry = now.Add(_options.LockTimeout);

        var sql = $"""
            WITH target AS (
                SELECT id
                FROM {_options.Schema}.outbox_messages
                WHERE processed_at_utc IS NULL
                    AND (locked_until_utc IS NULL OR locked_until_utc < @Now)
                ORDER BY id ASC
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            UPDATE {_options.Schema}.outbox_messages m
            SET locked_until_utc = @LockExpiry
            FROM target
            WHERE m.id = target.id
            RETURNING
                m.id AS {nameof(OutboxMessage.Id)},
                m.message_id AS {nameof(OutboxMessage.MessageId)},
                m.message_type AS {nameof(OutboxMessage.MessageType)},
                m.payload AS {nameof(OutboxMessage.Payload)},
                m.created_at_utc AS {nameof(OutboxMessage.CreatedAtUtc)},
                m.processed_at_utc AS {nameof(OutboxMessage.ProcessedAtUtc)},
                m.retry_count AS {nameof(OutboxMessage.RetryCount)},
                m.last_error AS {nameof(OutboxMessage.LastError)};
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                BatchSize = batchSize,
                Now = now,
                LockExpiry = lockExpiry,
            },
            cancellationToken: ct
        );

        var results = await connection.QueryAsync<OutboxMessage>(command);
        return results.AsList();
    }

    public async Task MarkCompletedAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var sql = $"""
            UPDATE {_options.Schema}.outbox_messages
            SET processed_at_utc = @Now,
                locked_until_utc = NULL
            WHERE id = @Id;
            """;

        var command = new CommandDefinition(
            sql,
            new { Id = id, Now = DateTimeOffset.UtcNow },
            cancellationToken: ct
        );

        await connection.ExecuteAsync(command);
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var sql = $"""
            UPDATE {_options.Schema}.outbox_messages
            SET retry_count = retry_count + 1,
                last_error = @Error,
                locked_until_utc = NULL,
                processed_at_utc = CASE
                    WHEN retry_count + 1 >= @MaxRetries THEN @Now
                    ELSE processed_at_utc
                END
            WHERE id = @Id;
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                Id = id,
                Error = error,
                _options.MaxRetries,
                Now = DateTimeOffset.UtcNow,
            },
            cancellationToken: ct
        );

        await connection.ExecuteAsync(command);
    }

    public async Task CleanupOldMessagesAsync(CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.Subtract(_options.CleanupAfter);

        var sql = $"""
            DELETE FROM {_options.Schema}.outbox_messages
            WHERE processed_at_utc IS NOT NULL
                AND processed_at_utc < @Cutoff;
            """;

        var command = new CommandDefinition(sql, new { Cutoff = cutoff }, cancellationToken: ct);
        var deleted = await connection.ExecuteAsync(command);

        if (deleted > 0)
        {
            logger.LogInformation("Cleaned up {Count} old outbox messages", deleted);
        }
    }
}
