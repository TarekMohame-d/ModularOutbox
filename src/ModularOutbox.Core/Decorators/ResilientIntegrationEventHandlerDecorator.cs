using System.Reflection;
using Microsoft.Extensions.Logging;
using ModularOutbox.Abstractions;
using ModularOutbox.Abstractions.Attributes;
using Polly.Registry;

namespace ModularOutbox.Core.Decorators;

internal sealed class ResilientIntegrationEventHandlerDecorator<TEvent>(
    IIntegrationEventHandler<TEvent> innerHandler,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<ResilientIntegrationEventHandlerDecorator<TEvent>> logger
) : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    public async Task HandleAsync(TEvent @event, CancellationToken ct = default)
    {
        var handlerType = innerHandler.GetType();

        var attribute =
            handlerType.GetCustomAttribute<ResilientHandlerAttribute>()
            ?? handlerType.GetMethod(nameof(HandleAsync))?.GetCustomAttribute<ResilientHandlerAttribute>();

        if (attribute is null)
        {
            await innerHandler.HandleAsync(@event, ct);
            return;
        }

        // Fetch named pipeline from provider
        if (!pipelineProvider.TryGetPipeline(attribute.PipelineName, out var pipeline))
        {
            logger.LogWarning(
                "Resilience pipeline '{PipelineName}' was configured on '{HandlerName}' but not registered in DI. Executing without micro-retries.",
                attribute.PipelineName,
                handlerType.Name
            );

            await innerHandler.HandleAsync(@event, ct);
            return;
        }

        await pipeline.ExecuteAsync(async token => await innerHandler.HandleAsync(@event, token), ct);
    }
}
