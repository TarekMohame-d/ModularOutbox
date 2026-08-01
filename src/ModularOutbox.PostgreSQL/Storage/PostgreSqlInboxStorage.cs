using Dapper;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Storage;
using Npgsql;

namespace ModularOutbox.PostgreSQL.Storage;

internal sealed class PostgreSqlInboxStorage(
    NpgsqlDataSource dataSource,
    IOptions<ModularOutboxOptions> options
) : IInboxStorage
{
    private readonly ModularOutboxOptions _options = options.Value;

    public async Task<bool> HasBeenProcessedAsync(
        Guid messageId,
        string consumerName,
        CancellationToken ct = default
    )
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var sql = $"""
            SELECT EXISTS (
                SELECT 1
                FROM {_options.Schema}.inbox_messages
                WHERE message_id = @MessageId AND consumer_name = @ConsumerName
            );
            """;

        var command = new CommandDefinition(
            sql,
            new { MessageId = messageId, ConsumerName = consumerName },
            cancellationToken: ct
        );

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task SaveAsync(Guid messageId, string consumerName, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var sql = $"""
            INSERT INTO {_options.Schema}.inbox_messages (message_id, consumer_name, processed_at_utc)
            VALUES (@MessageId, @ConsumerName, @Now)
            ON CONFLICT (message_id, consumer_name) DO NOTHING;
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                MessageId = messageId,
                ConsumerName = consumerName,
                Now = DateTimeOffset.UtcNow,
            },
            cancellationToken: ct
        );

        await connection.ExecuteAsync(command);
    }
}
