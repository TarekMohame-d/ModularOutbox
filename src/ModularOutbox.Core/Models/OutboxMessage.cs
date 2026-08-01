namespace ModularOutbox.Core.Models;

/// <summary>
/// Represents a message in the transactional outbox.
/// </summary>
internal sealed class OutboxMessage
{
    /// <summary>
    /// Auto-increment PK for FIFO ordering.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Unique message identifier for idempotency and cancellation.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Assembly-qualified type name for deserialization.
    /// </summary>
    public string MessageType { get; set; } = null!;

    /// <summary>
    /// JSON-serialized message payload.
    /// </summary>
    public string Payload { get; set; } = null!;

    /// <summary>
    /// When the message was enqueued.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// When the message was processed.
    /// </summary>
    public DateTimeOffset? ProcessedAtUtc { get; set; }

    /// <summary>
    /// When the message was locked.
    /// </summary>
    public DateTimeOffset? LockedUntilUtc { get; set; }

    /// <summary>
    /// Number of processing attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Last error message (for debugging).
    /// </summary>
    public string? LastError { get; set; }
}
