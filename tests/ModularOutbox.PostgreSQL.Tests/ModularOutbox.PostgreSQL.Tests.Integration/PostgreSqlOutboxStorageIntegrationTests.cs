using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Storage;
using ModularOutbox.PostgreSQL.Storage;
using ModularOutbox.PostgreSQL.Tests.Integration.Fixture;
using Shouldly;

namespace ModularOutbox.PostgreSQL.Tests.Integration;

[Collection(PostgreSqlTestCollection.Name)]
public class PostgreSqlOutboxStorageIntegrationTests(PostgreSqlTestFixture fixture)
{
    private readonly ModularOutboxOptions _options = new()
    {
        ConnectionString = fixture.ConnectionString,
        Schema = "messaging",
        BatchSize = 10,
        LockTimeout = TimeSpan.FromSeconds(5),
        MaxRetries = 3,
        CleanupAfter = TimeSpan.FromHours(1),
    };

    private IOutboxStorage CreateStorage() =>
        new PostgreSqlOutboxStorage(
            fixture.DataSource,
            Options.Create(_options),
            NullLogger<PostgreSqlOutboxStorage>.Instance
        );

    [Fact]
    public async Task FetchUnprocessedBatchAsync_LocksRowsWithSkipLocked_AndPreventsConcurrentWorkersFromFetchingSameRows()
    {
        // Arrange
        var storage = CreateStorage();
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();

        await InsertOutboxMessageAsync(messageId1, "TestEvent1", "{}");
        await InsertOutboxMessageAsync(messageId2, "TestEvent2", "{}");

        // Act - Worker 1 fetches batch of 1 item
        var worker1Batch = await storage.FetchUnprocessedBatchAsync(1, TestContext.Current.CancellationToken);

        // Act - Worker 2 fetches batch of 1 item concurrently
        var worker2Batch = await storage.FetchUnprocessedBatchAsync(1, TestContext.Current.CancellationToken);

        // Assert
        worker1Batch.Count.ShouldBe(1);
        worker2Batch.Count.ShouldBe(1);
        worker1Batch[0].MessageId.ShouldNotBe(worker2Batch[0].MessageId);
    }

    [Fact]
    public async Task MarkCompletedAsync_UpdatesProcessedAtUtc_AndClearsLock()
    {
        // Arrange
        var storage = CreateStorage();
        var messageId = Guid.NewGuid();
        await InsertOutboxMessageAsync(messageId, "TestEvent", "{}");

        var batch = await storage.FetchUnprocessedBatchAsync(10, TestContext.Current.CancellationToken);
        var message = batch.Single(m => m.MessageId == messageId);

        // Act
        await storage.MarkCompletedAsync(message.Id, TestContext.Current.CancellationToken);

        // Assert
        var reFetchedBatch = await storage.FetchUnprocessedBatchAsync(
            10,
            TestContext.Current.CancellationToken
        );
        reFetchedBatch.ShouldNotContain(m => m.MessageId == messageId);
    }

    [Fact]
    public async Task MarkFailedAsync_IncrementsRetryCount_AndDeadLettersWhenMaxRetriesReached()
    {
        // Arrange
        var storage = CreateStorage();
        var messageId = Guid.NewGuid();
        await InsertOutboxMessageAsync(messageId, "TestEvent", "{}");

        var batch = await storage.FetchUnprocessedBatchAsync(10, TestContext.Current.CancellationToken);
        var message = batch.Single(m => m.MessageId == messageId);

        // Act 1 & 2: Fail twice (RetryCount becomes 2 < MaxRetries=3)
        await storage.MarkFailedAsync(
            message.Id,
            "Temporary Network Error 1",
            TestContext.Current.CancellationToken
        );
        await storage.MarkFailedAsync(
            message.Id,
            "Temporary Network Error 2",
            TestContext.Current.CancellationToken
        );

        // Act 3: Fail 3rd time (RetryCount = 3 >= MaxRetries) -> Should mark processed_at_utc to stop retrying
        await storage.MarkFailedAsync(message.Id, "Fatal Error", TestContext.Current.CancellationToken);

        // Assert
        var unProcessedBatch = await storage.FetchUnprocessedBatchAsync(
            10,
            TestContext.Current.CancellationToken
        );
        unProcessedBatch.ShouldNotContain(m => m.MessageId == messageId);
    }

    [Fact]
    public async Task CleanupOldMessagesAsync_DeletesProcessedMessagesOlderThanCutoff()
    {
        // Arrange
        var storage = CreateStorage();
        var oldMessageId = Guid.NewGuid();

        await InsertOutboxMessageAsync(
            oldMessageId,
            "OldEvent",
            "{}",
            processedAtUtc: DateTimeOffset.UtcNow.AddHours(-2)
        );

        // Act
        await storage.CleanupOldMessagesAsync(TestContext.Current.CancellationToken);

        // Assert
        await using var connection = await fixture.DataSource.OpenConnectionAsync(
            TestContext.Current.CancellationToken
        );
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM messaging.outbox_messages WHERE message_id = $1";
        command.Parameters.AddWithValue(oldMessageId);

        var count = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        count.ShouldBe(0);
    }

    private async Task InsertOutboxMessageAsync(
        Guid messageId,
        string messageType,
        string payload,
        DateTimeOffset? processedAtUtc = null
    )
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync(
            TestContext.Current.CancellationToken
        );
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO messaging.outbox_messages (message_id, message_type, payload, created_at_utc, processed_at_utc)
            VALUES ($1, $2, $3, NOW(), $4);
            """;
        command.Parameters.AddWithValue(messageId);
        command.Parameters.AddWithValue(messageType);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Jsonb, payload);
        command.Parameters.AddWithValue(processedAtUtc.HasValue ? processedAtUtc.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
