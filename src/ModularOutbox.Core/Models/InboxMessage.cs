namespace ModularOutbox.Core.Models;

public sealed class InboxMessage
{
    public Guid Id { get; private set; }
    public string ConsumerName { get; private set; } = default!;
    public DateTimeOffset ProcessedAtUtc { get; private set; }

    private InboxMessage() { }

    public InboxMessage(Guid id, string consumerName)
    {
        Id = id;
        ConsumerName = consumerName;
        ProcessedAtUtc = DateTimeOffset.UtcNow;
    }
}
