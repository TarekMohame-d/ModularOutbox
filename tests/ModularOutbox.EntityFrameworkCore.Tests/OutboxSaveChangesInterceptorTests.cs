using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Context;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Services;
using ModularOutbox.EntityFrameworkCore.Interceptors;
using Shouldly;

namespace ModularOutbox.EntityFrameworkCore.Tests;

public record SampleDomainEvent(string Title) : IntegrationEvent;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}

public class OutboxSaveChangesInterceptorTests
{
    private readonly OutboxMessageContext _outboxContext = new();
    private readonly OutboxNotification _notification = new();
    private readonly OutboxTypeResolver _typeResolver = new();
    private readonly IOptions<ModularOutboxOptions> _options = Options.Create(new ModularOutboxOptions());

    private TestDbContext CreateDbContext()
    {
        var interceptor = new OutboxSaveChangesInterceptor(
            _outboxContext,
            _notification,
            _typeResolver,
            _options
        );

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEventsAreStaged_CreatesOutboxMessageAndSignalsNotification()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        var @event = new SampleDomainEvent("Item Created");
        _outboxContext.Stage(@event);

        _outboxContext.StagedEvents.Count.ShouldBe(1);

        // Act
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert

        // Events were drained
        _outboxContext.StagedEvents.ShouldBeEmpty();

        // SavedChangesAsync clears the pending notification
        _outboxContext.HasPendingNotification.ShouldBeFalse();

        // Outbox message was added
        var outboxMessage = await dbContext.OutboxMessages.SingleAsync(
            cancellationToken: TestContext.Current.CancellationToken
        );

        outboxMessage.MessageId.ShouldBe(@event.Id);
        outboxMessage.Payload.ShouldContain("Item Created");

        // Notification was fired
        var signaled = await _notification.WaitAsync(
            TimeSpan.FromMilliseconds(100),
            TestContext.Current.CancellationToken
        );

        signaled.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNoEventsAreStaged_DoesNothing()
    {
        // Arrange
        await using var dbContext = CreateDbContext();

        // Act
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        _outboxContext.StagedEvents.ShouldBeEmpty();
        _outboxContext.HasPendingNotification.ShouldBeFalse();

        (
            await dbContext.OutboxMessages.CountAsync(
                cancellationToken: TestContext.Current.CancellationToken
            )
        ).ShouldBe(0);

        var signaled = await _notification.WaitAsync(
            TimeSpan.FromMilliseconds(100),
            TestContext.Current.CancellationToken
        );

        signaled.ShouldBeFalse();
    }
}
