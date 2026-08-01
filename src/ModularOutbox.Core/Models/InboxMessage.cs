namespace ModularOutbox.Core.Models;

internal sealed class InboxMessage
{
    /// <summary>
    /// Unique message identifier for idempotency and cancellation.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Name of the consumer that processed the message.
    /// </summary>
    public string ConsumerName { get; set; } = default!;

    /// <summary>
    /// When the message was processed.
    /// </summary>
    public DateTimeOffset ProcessedAtUtc { get; set; }
}
