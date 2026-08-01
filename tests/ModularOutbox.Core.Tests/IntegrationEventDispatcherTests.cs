using Microsoft.Extensions.DependencyInjection;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Dispatchers;
using NSubstitute;
using Shouldly;

namespace ModularOutbox.Core.Tests;

public record UserCreatedEvent(Guid UserId, string Email) : IntegrationEvent;

public class IntegrationEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WhenHandlersRegistered_InvokesAllHandlersInScopedProvider()
    {
        // Arrange
        var handler1 = Substitute.For<IIntegrationEventHandler<UserCreatedEvent>>();
        var handler2 = Substitute.For<IIntegrationEventHandler<UserCreatedEvent>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => handler1);
        services.AddScoped(_ => handler2);
        var serviceProvider = services.BuildServiceProvider();

        var sut = new IntegrationEventDispatcher(serviceProvider);
        var @event = new UserCreatedEvent(Guid.NewGuid(), "test@example.com");

        // Act
        await sut.DispatchAsync(@event, TestContext.Current.CancellationToken);

        // Assert
        await handler1.Received(1).HandleAsync(@event, Arg.Any<CancellationToken>());
        await handler2.Received(1).HandleAsync(@event, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WhenNoHandlersRegistered_ExecutesWithoutException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var sut = new IntegrationEventDispatcher(serviceProvider);
        var @event = new UserCreatedEvent(Guid.NewGuid(), "test@example.com");

        // Act & Assert
        await Should.NotThrowAsync(async () =>
            await sut.DispatchAsync(@event, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DispatchAsync_WhenEventIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var sut = new IntegrationEventDispatcher(serviceProvider);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.DispatchAsync<UserCreatedEvent>(null!, TestContext.Current.CancellationToken)
        );
    }
}
