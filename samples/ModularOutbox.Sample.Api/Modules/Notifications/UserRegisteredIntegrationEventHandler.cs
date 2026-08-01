using ModularOutbox.Abstractions;
using ModularOutbox.Abstractions.Attributes;
using ModularOutbox.Sample.Api.Modules.Identity;

namespace ModularOutbox.Sample.Api.Modules.Notifications;

[ResilientHandler("test")]
public sealed class UserRegisteredIntegrationEventHandler(
    IInboxStore inboxStore,
    ILogger<UserRegisteredIntegrationEventHandler> logger
) : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private const string ConsumerName = nameof(UserRegisteredIntegrationEventHandler);

    public async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken ct = default
    )
    {
        // 1. Idempotency Check
        if (await inboxStore.HasBeenProcessedAsync(integrationEvent.Id, ConsumerName, ct))
        {
            logger.LogInformation(
                "Event {Id} was already processed by {Consumer}. Skipping...",
                integrationEvent.Id,
                ConsumerName
            );
            return;
        }

        // 2. Perform Business Logic (e.g., Send Welcome Email)
        logger.LogInformation(
            ">>> [Notifications Module] Sending welcome email to {Email} (User ID: {UserId})",
            integrationEvent.Email,
            integrationEvent.UserId
        );

        // 3. Mark in Inbox Store
        await inboxStore.MarkAsProcessedAsync(integrationEvent.Id, ConsumerName, ct);
    }
}
