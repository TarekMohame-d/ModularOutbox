using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Dispatchers;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Storage;

namespace ModularOutbox.Core.Services;

internal sealed class OutboxDeliveryService(
    IServiceProvider serviceProvider,
    OutboxNotification notification,
    IOutboxTypeResolver typeResolver,
    IOptions<ModularOutboxOptions> options,
    ILogger<OutboxDeliveryService> logger
) : BackgroundService
{
    private readonly ModularOutboxOptions _options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox delivery service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                notification.Clear();

                var processedCount = await ProcessPendingMessagesAsync(stoppingToken);

                if (processedCount < _options.BatchSize)
                {
                    // If no messages were processed in the last batch, wait for an instant channel signal
                    // OR fall back to the configured polling interval timeout.
                    await notification.WaitAsync(_options.PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Outbox delivery service stopped");
    }

    private async Task<int> ProcessPendingMessagesAsync(CancellationToken ct)
    {
        logger.LogInformation("Processing outbox messages at {Timestamp}", DateTimeOffset.UtcNow);

        List<OutboxMessage> messages;
        await using (var fetchScope = serviceProvider.CreateAsyncScope())
        {
            var storage = fetchScope.ServiceProvider.GetRequiredService<IOutboxStorage>();
            messages = (await storage.FetchUnprocessedBatchAsync(_options.BatchSize, ct)).ToList();
        }

        if (messages.Count == 0)
            return 0;

        foreach (var message in messages)
        {
            if (ct.IsCancellationRequested)
                break;

            // Fresh, isolated DI Scope per message processing
            await using var messageScope = serviceProvider.CreateAsyncScope();
            var storage = messageScope.ServiceProvider.GetRequiredService<IOutboxStorage>();

            await ProcessMessageAsync(messageScope.ServiceProvider, storage, message, ct);
        }

        return messages.Count;
    }

    private async Task ProcessMessageAsync(
        IServiceProvider scopedProvider,
        IOutboxStorage storage,
        OutboxMessage message,
        CancellationToken ct
    )
    {
        try
        {
            // Deserialize message payload
            var messageType = typeResolver.ResolveType(message.MessageType);

            var payload =
                JsonSerializer.Deserialize(message.Payload, messageType, _options.JsonSerializerOptions)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize payload for message {message.MessageId}"
                );

            if (payload is not IIntegrationEvent integrationEvent)
            {
                throw new InvalidOperationException(
                    $"Payload of type '{messageType.Name}' does not inherit from IIntegrationEvent"
                );
            }

            var dispatcher = scopedProvider.GetRequiredService<IIntegrationEventDispatcher>();
            await dispatcher.DispatchAsync(integrationEvent, ct);

            await storage.MarkCompletedAsync(message.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing outbox message {MessageId}", message.MessageId);

            await storage.MarkFailedAsync(message.Id, ex.Message, ct);
        }
    }
}
