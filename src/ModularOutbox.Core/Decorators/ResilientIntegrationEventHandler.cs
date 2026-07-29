using Microsoft.Extensions.Logging;
using ModularOutbox.Abstractions;
using Polly;
using Polly.Retry;

namespace ModularOutbox.Core.Decorators;

public sealed class ResilientIntegrationEventHandler<TEvent>(
    IIntegrationEventHandler<TEvent> innerHandler,
    ILogger<ResilientIntegrationEventHandler<TEvent>> logger
) : IIntegrationEventHandler<TEvent>
    where TEvent : class, IIntegrationEvent
{
    private static readonly ResiliencePipeline Pipeline = new ResiliencePipelineBuilder()
        .AddRetry(
            new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            }
        )
        .Build();

    public async Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default)
    {
        await Pipeline.ExecuteAsync(
            async cancellationToken =>
            {
                try
                {
                    await innerHandler.HandleAsync(integrationEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to process event {EventId} of type {EventType} in handler {HandlerType}. Retrying...",
                        integrationEvent.Id,
                        typeof(TEvent).Name,
                        innerHandler.GetType().Name
                    );
                    throw;
                }
            },
            ct
        );
    }
}
