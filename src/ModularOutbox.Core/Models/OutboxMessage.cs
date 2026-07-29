namespace ModularOutbox.Core.Models;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public bool DeadLetter { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAtUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    public void MarkProcessed()
    {
        ProcessedAtUtc = DateTimeOffset.UtcNow;
        Error = null;
    }

    public void HandleFailure(string errorMessage, int maxRetries)
    {
        RetryCount++;
        Error = errorMessage;

        if (RetryCount >= maxRetries)
            DeadLetter = true;
    }
}
