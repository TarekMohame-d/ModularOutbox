namespace ModularOutbox.Abstractions;

/// <summary>
/// Represents the fundamental contract for an integration event published across application boundaries
/// or modules via the outbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// Integration events represent past occurrences in the system that other bounded contexts,
/// microservices, or modules may react to.
/// </para>
/// <para>
/// Implementations of this interface serve as the data payload persisted by outbox stores and distributed
/// to inbox stores. The properties provided by this contract enable message deduplication, auditing,
/// and strict event sequencing.
/// </para>
/// </remarks>
/// <example>
/// The following example demonstrates implementing <see cref="IIntegrationEvent"/> using C# records:
/// <code>
/// public sealed record OrderPlacedIntegrationEvent(
///     Guid OrderId,
///     string CustomerId,
///     decimal TotalAmount
/// ) : IIntegrationEvent
/// {
///     public Guid Id { get; init; } = Guid.NewGuid();
///     public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
/// }
/// </code>
/// </example>
public interface IIntegrationEvent
{
    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> that uniquely identifies this specific event execution, used for deduplication
    /// and idempotency tracking in inbox stores.
    /// </value>
    Guid Id { get; }

    /// <summary>
    /// Gets the date and time (in UTC) when the event occurred.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> representing the exact point in time in UTC when the event was generated.
    /// </value>
    DateTimeOffset OccurredAtUtc { get; }
}
