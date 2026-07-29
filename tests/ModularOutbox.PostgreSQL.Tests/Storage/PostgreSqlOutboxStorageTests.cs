using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;
using ModularOutbox.PostgreSQL.Persistence;
using ModularOutbox.PostgreSQL.Storage;
using ModularOutbox.PostgreSQL.Tests.Fixtures;
using Shouldly;

namespace ModularOutbox.PostgreSQL.Tests.Storage;

[Collection("PostgreSQL")]
public class PostgreSqlOutboxStorageTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private readonly IOptions<ModularOutboxOptions> _options = Options.Create(new ModularOutboxOptions());

    public async ValueTask InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private MessagingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        var outboxOptions = Options.Create(new ModularOutboxOptions());

        var context = new MessagingDbContext(options, outboxOptions);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task FetchUnprocessedBatchAsync_ReturnsOnlyUnprocessedMessagesUpToBatchSize()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var storage = new PostgreSqlOutboxStorage(dbContext, _options);

        // Seed 5 messages (3 pending, 1 processed, 1 dead-letter)
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var msg1 = new OutboxMessage(Guid.NewGuid(), "OrderCreated", "{}", occurredAtUtc);
        var msg2 = new OutboxMessage(Guid.NewGuid(), "OrderCreated", "{}", occurredAtUtc);
        var msg3 = new OutboxMessage(Guid.NewGuid(), "OrderCreated", "{}", occurredAtUtc);
        var msgProcessed = new OutboxMessage(Guid.NewGuid(), "OrderCreated", "{}", occurredAtUtc);
        msgProcessed.MarkProcessed();

        var msgDeadLetter = new OutboxMessage(Guid.NewGuid(), "OrderCreated", "{}", occurredAtUtc);
        msgDeadLetter.HandleFailure("Fatal Error", maxRetries: 1);

        dbContext.OutboxMessages.AddRange(msg1, msg2, msg3, msgProcessed, msgDeadLetter);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var batch = await storage.FetchUnprocessedBatchAsync(
            batchSize: 2,
            ct: TestContext.Current.CancellationToken
        );

        // Assert
        batch.Count.ShouldBe(2);
        batch.ShouldAllBe(m => m.ProcessedAtUtc == null && !m.DeadLetter);
    }

    [Fact]
    public async Task FetchUnprocessedBatchAsync_ConcurrentTransactions_SkipsLockedRows()
    {
        // Arrange
        await using var dbContext1 = CreateDbContext();
        await using var dbContext2 = CreateDbContext();

        var occurredAtUtc = DateTimeOffset.UtcNow;
        var msg = new OutboxMessage(Guid.NewGuid(), "OrderCreated", "{}", occurredAtUtc);
        dbContext1.OutboxMessages.Add(msg);
        await dbContext1.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        // Transaction 1 locks the row
        await using var tx1 = await dbContext1.Database.BeginTransactionAsync(
            TestContext.Current.CancellationToken
        );
        var storage1 = new PostgreSqlOutboxStorage(dbContext1, _options);
        var batch1 = await storage1.FetchUnprocessedBatchAsync(10, TestContext.Current.CancellationToken);
        batch1.Count.ShouldBe(1);

        // Transaction 2 tries to fetch while TX1 holds the lock -> SKIP LOCKED should yield empty
        var storage2 = new PostgreSqlOutboxStorage(dbContext2, _options);
        var batch2 = await storage2.FetchUnprocessedBatchAsync(10, TestContext.Current.CancellationToken);
        batch2.ShouldBeEmpty(); // Locked row was skipped safely!

        await tx1.RollbackAsync(TestContext.Current.CancellationToken);
    }

    // =========================================================================
    // 1. BOUNDARY & EDGE CASE TESTS
    // =========================================================================

    [Fact]
    public async Task FetchUnprocessedBatchAsync_BoundaryBatchSizes_HandlesZeroOneAndExcessiveBatchSizesCorrectly()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var storage = new PostgreSqlOutboxStorage(dbContext, _options);
        var now = DateTimeOffset.UtcNow;

        var messages = Enumerable
            .Range(1, 5)
            .Select(i => new OutboxMessage(Guid.NewGuid(), $"Event_{i}", "{}", now))
            .ToList();

        dbContext.OutboxMessages.AddRange(messages);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act 1: Batch size 0 should return empty
        var batchZero = await storage.FetchUnprocessedBatchAsync(0, TestContext.Current.CancellationToken);
        batchZero.ShouldBeEmpty();

        // Act 2: Batch size 1 should return exactly 1 message
        var batchOne = await storage.FetchUnprocessedBatchAsync(1, TestContext.Current.CancellationToken);
        batchOne.Count.ShouldBe(1);

        // Act 3: Requesting 100 when only 5 exist should gracefully return all 5 without error
        var batchExcessive = await storage.FetchUnprocessedBatchAsync(
            100,
            TestContext.Current.CancellationToken
        );
        batchExcessive.Count.ShouldBe(5);
    }

    [Fact]
    public async Task FetchUnprocessedBatchAsync_StrictFIFOOrdering_ReturnsMessagesInChronologicalOrder()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var storage = new PostgreSqlOutboxStorage(dbContext, _options);
        var baseTime = DateTimeOffset.UtcNow;

        // Seed messages out-of-order chronologically
        var msg3 = new OutboxMessage(Guid.NewGuid(), "Event_3", "{}", baseTime.AddMinutes(3));
        var msg1 = new OutboxMessage(Guid.NewGuid(), "Event_1", "{}", baseTime.AddMinutes(1)); // Oldest
        var msg2 = new OutboxMessage(Guid.NewGuid(), "Event_2", "{}", baseTime.AddMinutes(2));

        dbContext.OutboxMessages.AddRange(msg3, msg1, msg2);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var batch = await storage.FetchUnprocessedBatchAsync(10, TestContext.Current.CancellationToken);

        // Assert: Must be returned in strict chronological order (msg1 -> msg2 -> msg3)
        batch.Count.ShouldBe(3);
        batch[0].Id.ShouldBe(msg1.Id);
        batch[1].Id.ShouldBe(msg2.Id);
        batch[2].Id.ShouldBe(msg3.Id);
    }

    // =========================================================================
    // 2. STRESS & HIGH CONCURRENCY TESTS
    // =========================================================================

    [Fact]
    public async Task FetchUnprocessedBatchAsync_HighWorkerContention_GuaranteesZeroDuplicateLocks()
    {
        // Arrange: Seed 100 messages
        await using var initContext = CreateDbContext();
        var baseTime = DateTimeOffset.UtcNow;
        const int totalMessages = 100;

        var messages = Enumerable
            .Range(0, totalMessages)
            .Select(i => new OutboxMessage(Guid.NewGuid(), "OrderCreated", "{}", baseTime.AddMilliseconds(i)))
            .ToList();

        initContext.OutboxMessages.AddRange(messages);
        await initContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        const int workerCount = 10;
        const int batchSize = 10;

        // Phase 1: Fire all workers simultaneously for peak SQL contention
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Phase 2: Hold transactions open until assertions complete
        var releaseSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workerPairs = Enumerable
            .Range(0, workerCount)
            .Select(_ =>
            {
                var fetchCompleted = new TaskCompletionSource<List<Guid>>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );

                var workerTask = Task.Run(async () =>
                {
                    await using var workerDbContext = CreateDbContext();
                    var workerStorage = new PostgreSqlOutboxStorage(workerDbContext, _options);

                    await startSignal.Task;

                    await using var tx = await workerDbContext.Database.BeginTransactionAsync(
                        TestContext.Current.CancellationToken
                    );

                    var batch = await workerStorage.FetchUnprocessedBatchAsync(
                        batchSize,
                        TestContext.Current.CancellationToken
                    );

                    // Send fetched IDs back to the test thread
                    fetchCompleted.SetResult(batch.Select(m => m.Id).ToList());

                    // Hold transaction and row locks open until main thread finishes assertions
                    await releaseSignal.Task;
                });

                return (FetchTask: fetchCompleted.Task, WorkerTask: workerTask);
            })
            .ToList();

        // Act 1: Release all workers to execute SQL queries concurrently
        startSignal.SetResult();

        // Act 2: Wait for all 10 workers to fetch their batch while transactions REMAIN OPEN
        var batchResults = await Task.WhenAll(workerPairs.Select(p => p.FetchTask));

        // Assert: Every message locked across all workers must be unique (Zero duplication)
        var allLockedIds = batchResults.SelectMany(ids => ids).ToList();

        allLockedIds.Count.ShouldBe(
            totalMessages,
            "10 workers pulling batches of 10 should lock all 100 rows"
        );
        allLockedIds
            .Distinct()
            .Count()
            .ShouldBe(totalMessages, "SKIP LOCKED failed! Duplicate rows were claimed by multiple workers.");

        // Cleanup: Release transactions and wait for worker tasks to dispose cleanly
        releaseSignal.SetResult();
        await Task.WhenAll(workerPairs.Select(p => p.WorkerTask));
    }

    [Fact]
    public async Task FetchUnprocessedBatchAsync_MultiWorkerQueueDrain_ProcessesAllMessagesWithoutDataLoss()
    {
        // Arrange: Seed 250 outbox messages
        await using var initContext = CreateDbContext();
        var baseTime = DateTimeOffset.UtcNow;
        const int totalMessages = 250;

        var messages = Enumerable
            .Range(0, totalMessages)
            .Select(i => new OutboxMessage(Guid.NewGuid(), "BulkEvent", "{}", baseTime.AddMilliseconds(i)))
            .ToList();

        initContext.OutboxMessages.AddRange(messages);
        await initContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 5 parallel workers repeatedly fetch batch of 15, mark processed, and commit until database is empty
        const int workerCount = 5;
        const int batchSize = 15;
        var processedCount = 0;

        var workers = Enumerable
            .Range(0, workerCount)
            .Select(async _ =>
            {
                int localProcessed = 0;

                while (!TestContext.Current.CancellationToken.IsCancellationRequested)
                {
                    await using var workerDbContext = CreateDbContext();
                    var workerStorage = new PostgreSqlOutboxStorage(workerDbContext, _options);

                    await using var tx = await workerDbContext.Database.BeginTransactionAsync(
                        TestContext.Current.CancellationToken
                    );
                    var batch = await workerStorage.FetchUnprocessedBatchAsync(
                        batchSize,
                        TestContext.Current.CancellationToken
                    );

                    if (batch.Count == 0)
                    {
                        await tx.RollbackAsync(TestContext.Current.CancellationToken);
                        break; // Outbox drained
                    }

                    foreach (var msg in batch)
                    {
                        msg.MarkProcessed();
                    }

                    await workerDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                    await tx.CommitAsync(TestContext.Current.CancellationToken);

                    localProcessed += batch.Count;
                }

                Interlocked.Add(ref processedCount, localProcessed);
            });

        // Act
        await Task.WhenAll(workers);

        // Assert
        processedCount.ShouldBe(
            totalMessages,
            "All messages should be processed exactly once across workers"
        );

        await using var verifyContext = CreateDbContext();
        var remainingUnprocessed = await verifyContext.OutboxMessages.CountAsync(
            m => m.ProcessedAtUtc == null && !m.DeadLetter,
            TestContext.Current.CancellationToken
        );

        remainingUnprocessed.ShouldBe(0, "No unprocessed messages should remain in the database");
    }

    [Fact]
    public async Task FetchUnprocessedBatchAsync_ConcurrentProducerAndConsumers_HandlesInterleavedReadsAndWrites()
    {
        // Arrange: Producer continuously inserts messages while 3 consumers continuously pull and mark processed
        const int totalToProduce = 60;
        var cancellationToken = TestContext.Current.CancellationToken;

        // Producer Task
        var producerTask = Task.Run(
            async () =>
            {
                for (int i = 0; i < totalToProduce; i++)
                {
                    await using var producerContext = CreateDbContext();
                    var msg = new OutboxMessage(Guid.NewGuid(), "StreamEvent", "{}", DateTimeOffset.UtcNow);
                    producerContext.OutboxMessages.Add(msg);
                    await producerContext.SaveChangesAsync(cancellationToken);

                    await Task.Delay(5, cancellationToken); // Yield briefly between writes
                }
            },
            cancellationToken
        );

        // Consumer Tasks
        var totalConsumed = 0;
        var consumerTasks = Enumerable
            .Range(0, 3)
            .Select(async _ =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await using var consumerContext = CreateDbContext();
                    var storage = new PostgreSqlOutboxStorage(consumerContext, _options);

                    await using var tx = await consumerContext.Database.BeginTransactionAsync(
                        cancellationToken
                    );
                    var batch = await storage.FetchUnprocessedBatchAsync(10, cancellationToken);

                    if (batch.Count > 0)
                    {
                        foreach (var m in batch)
                            m.MarkProcessed();
                        await consumerContext.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);

                        Interlocked.Add(ref totalConsumed, batch.Count);
                    }
                    else
                    {
                        await tx.RollbackAsync(cancellationToken);

                        // Exit condition: Producer finished and total consumed reached total produced
                        if (
                            producerTask.IsCompletedSuccessfully
                            && Volatile.Read(ref totalConsumed) >= totalToProduce
                        )
                        {
                            break;
                        }

                        await Task.Delay(10, cancellationToken); // Wait for new inserts
                    }
                }
            })
            .ToList();

        // Act
        await Task.WhenAll(consumerTasks.Concat([producerTask]));

        // Assert
        totalConsumed.ShouldBe(totalToProduce);

        await using var verifyContext = CreateDbContext();
        var unprocessedCount = await verifyContext.OutboxMessages.CountAsync(
            m => m.ProcessedAtUtc == null && !m.DeadLetter,
            cancellationToken
        );

        unprocessedCount.ShouldBe(0);
    }
}
