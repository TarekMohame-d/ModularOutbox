using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Storage;

namespace ModularOutbox.Core.Processing;

public sealed class OutboxProcessor(
    IOutboxStorage storage,
    IIntegrationEventDispatcher dispatcher,
    IOptions<ModularOutboxOptions> options,
    ILogger<OutboxProcessor> logger
) : IOutboxProcessor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        var batchSize = options.Value.BatchSize;
        var maxRetries = options.Value.MaxRetries;

        var messages = await storage.FetchUnprocessedBatchAsync(batchSize, ct);

        if (messages.Count == 0)
            return 0;

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type);
                if (eventType is null)
                {
                    logger.LogError(
                        "Could not resolve type {Type} for outbox message {Id}",
                        message.Type,
                        message.Id
                    );
                    message.HandleFailure($"Could not resolve type: {message.Type}", maxRetries);
                    continue;
                }

                var deserializedObject = JsonSerializer.Deserialize(
                    message.Payload,
                    eventType,
                    SerializerOptions
                );
                if (deserializedObject is not IIntegrationEvent integrationEvent)
                {
                    logger.LogError("Failed to deserialize payload for outbox message {Id}", message.Id);
                    message.HandleFailure("Payload deserialization returned null", maxRetries);
                    continue;
                }

                await dispatcher.DispatchAsync(integrationEvent, ct);
                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing outbox message {Id}", message.Id);
                message.HandleFailure(ex.Message, maxRetries);
            }
        }

        await storage.SaveChangesAsync(ct);
        return messages.Count;
    }
}
