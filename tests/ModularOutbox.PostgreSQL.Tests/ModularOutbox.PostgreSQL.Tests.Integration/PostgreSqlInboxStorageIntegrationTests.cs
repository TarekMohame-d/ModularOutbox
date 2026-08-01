using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using ModularOutbox.PostgreSQL.Storage;
using ModularOutbox.PostgreSQL.Tests.Integration.Fixture;
using Shouldly;

namespace ModularOutbox.PostgreSQL.Tests.Integration;

[Collection(PostgreSqlTestCollection.Name)]
public class PostgreSqlInboxStorageIntegrationTests(PostgreSqlTestFixture fixture)
{
    private PostgreSqlInboxStorage CreateStorage()
    {
        var options = Options.Create(
            new ModularOutboxOptions { ConnectionString = fixture.ConnectionString, Schema = "messaging" }
        );

        return new PostgreSqlInboxStorage(fixture.DataSource, options);
    }

    [Fact]
    public async Task HasBeenProcessedAsync_WhenNotProcessed_ReturnsFalse()
    {
        // Arrange
        var storage = CreateStorage();
        var messageId = Guid.NewGuid();

        // Act
        var result = await storage.HasBeenProcessedAsync(
            messageId,
            "ConsumerA",
            TestContext.Current.CancellationToken
        );

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_MarksMessageProcessedForSpecificConsumer_AndMaintainsConsumerIsolation()
    {
        // Arrange
        var storage = CreateStorage();
        var messageId = Guid.NewGuid();

        // Act
        await storage.SaveAsync(messageId, "ConsumerA", TestContext.Current.CancellationToken);

        // Assert
        var processedForConsumerA = await storage.HasBeenProcessedAsync(
            messageId,
            "ConsumerA",
            TestContext.Current.CancellationToken
        );
        var processedForConsumerB = await storage.HasBeenProcessedAsync(
            messageId,
            "ConsumerB",
            TestContext.Current.CancellationToken
        );

        processedForConsumerA.ShouldBeTrue();
        processedForConsumerB.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenCalledDuplicateTimes_HandlesOnConflictGracefullyWithoutThrowing()
    {
        // Arrange
        var storage = CreateStorage();
        var messageId = Guid.NewGuid();

        // Act & Assert
        await Should.NotThrowAsync(async () =>
        {
            await storage.SaveAsync(messageId, "ConsumerA", TestContext.Current.CancellationToken);
            await storage.SaveAsync(messageId, "ConsumerA", TestContext.Current.CancellationToken);
        });
    }
}
