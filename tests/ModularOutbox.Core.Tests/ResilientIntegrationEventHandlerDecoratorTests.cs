using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModularOutbox.Abstractions;
using ModularOutbox.Abstractions.Attributes;
using ModularOutbox.Core.Decorators;
using NSubstitute;
using Polly;
using Polly.Registry;

namespace ModularOutbox.Core.Tests;

public record PaymentFailedEvent(Guid PaymentId) : IntegrationEvent;

[ResilientHandler("PaymentResiliencePipeline")]
public class ResilientPaymentHandler : IIntegrationEventHandler<PaymentFailedEvent>
{
    public virtual Task HandleAsync(PaymentFailedEvent @event, CancellationToken ct = default) =>
        Task.CompletedTask;
}

public class NonResilientPaymentHandler : IIntegrationEventHandler<PaymentFailedEvent>
{
    public virtual Task HandleAsync(PaymentFailedEvent @event, CancellationToken ct = default) =>
        Task.CompletedTask;
}

public class ResilientIntegrationEventHandlerDecoratorTests
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider = Substitute.For<
        ResiliencePipelineProvider<string>
    >();
    private readonly ILogger<ResilientIntegrationEventHandlerDecorator<PaymentFailedEvent>> _logger =
        NullLogger<ResilientIntegrationEventHandlerDecorator<PaymentFailedEvent>>.Instance;

    [Fact]
    public async Task HandleAsync_WhenNoResilientAttribute_InvokesInnerHandlerDirectly()
    {
        // Arrange
        var innerHandler = Substitute.For<NonResilientPaymentHandler>();
        var sut = new ResilientIntegrationEventHandlerDecorator<PaymentFailedEvent>(
            innerHandler,
            _pipelineProvider,
            _logger
        );
        var @event = new PaymentFailedEvent(Guid.NewGuid());

        // Act
        await sut.HandleAsync(@event, TestContext.Current.CancellationToken);

        // Assert
        await innerHandler.Received(1).HandleAsync(@event, Arg.Any<CancellationToken>());
        _pipelineProvider.DidNotReceiveWithAnyArgs().GetPipeline(default!);
    }

    [Fact]
    public async Task HandleAsync_WhenResilientAttributePresentAndPipelineExists_ExecutesThroughPipeline()
    {
        // Arrange
        var innerHandler = Substitute.For<ResilientPaymentHandler>();
        var pipeline = new ResiliencePipelineBuilder().Build();

        _pipelineProvider
            .TryGetPipeline("PaymentResiliencePipeline", out Arg.Any<ResiliencePipeline>()!)
            .Returns(x =>
            {
                x[1] = pipeline;
                return true;
            });

        var sut = new ResilientIntegrationEventHandlerDecorator<PaymentFailedEvent>(
            innerHandler,
            _pipelineProvider,
            _logger
        );
        var @event = new PaymentFailedEvent(Guid.NewGuid());

        // Act
        await sut.HandleAsync(@event, TestContext.Current.CancellationToken);

        // Assert
        await innerHandler.Received(1).HandleAsync(@event, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenPipelineNotRegisteredInDI_LogsWarningAndFallbackExecutesInnerHandler()
    {
        // Arrange
        var innerHandler = Substitute.For<ResilientPaymentHandler>();
        _pipelineProvider
            .TryGetPipeline(Arg.Any<string>(), out Arg.Any<ResiliencePipeline>()!)
            .Returns(false);

        var sut = new ResilientIntegrationEventHandlerDecorator<PaymentFailedEvent>(
            innerHandler,
            _pipelineProvider,
            _logger
        );
        var @event = new PaymentFailedEvent(Guid.NewGuid());

        // Act
        await sut.HandleAsync(@event, TestContext.Current.CancellationToken);

        // Assert
        await innerHandler.Received(1).HandleAsync(@event, Arg.Any<CancellationToken>());
    }
}
