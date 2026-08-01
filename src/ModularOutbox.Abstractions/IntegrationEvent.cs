namespace ModularOutbox.Abstractions;

/// <summary>
/// Serves as the foundational base record for all integration events, providing value-based equality,
/// automatic sequential UUIDv7 identifier generation, and UTC timestamping.
/// </summary>
/// <remarks>
/// <para>
/// Derived types should inherit from this base record to fulfill the <see cref="IIntegrationEvent"/> contract.
/// Because this is a C# <see langword="record"/>, derived instances automatically benefit from value-based equality
/// and immutability.
/// </para>
/// <para>
/// <b>Database Indexing &amp; Performance:</b><br/>
/// By default, the parameterless constructor generates identifiers using UUIDv7 (<see cref="Guid.CreateVersion7()"/>).
/// Time-ordered GUIDs significantly improve database indexing performance and reduce page fragmentation in outbox/inbox persistence stores.
/// </para>
/// </remarks>
/// <example>
/// The following example demonstrates inheriting from <see cref="IntegrationEvent"/> to define a custom domain contract:
/// <code>
/// public sealed record OrderShippedEvent(
///     Guid OrderId,
///     string TrackingNumber
/// ) : IntegrationEvent;
///
/// // Usage: Automatically gets a UUIDv7 Id and UTC OccurredAtUtc
/// var @event = new OrderShippedEvent(Guid.NewGuid(), "TRACK-12345");
/// </code>
/// </example>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// Gets the unique identifier for this integration event.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> representing the event identity. Defaults to a time-ordered UUIDv7.
    /// </value>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the date and time (in UTC) when this event instance was created.
    /// </summary>
    /// <value>
    /// A <see cref="DateTimeOffset"/> representing the creation point in UTC.
    /// </value>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEvent"/> record with an auto-generated
    /// time-ordered UUIDv7 <see cref="Id"/> and the current UTC timestamp for <see cref="OccurredAtUtc"/>.
    /// </summary>
    protected IntegrationEvent()
    {
        Id = Guid.CreateVersion7();
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEvent"/> record with explicit identifier
    /// and timestamp values.
    /// </summary>
    /// <param name="id">The explicit <see cref="Guid"/> identifier to assign to the event.</param>
    /// <param name="occurredAtUtc">The explicit UTC timestamp to assign to the event.</param>
    /// <remarks>
    /// This constructor is primarily used during message rehydration or deserialization when restoring
    /// an existing event payload from a database store or message bus.
    /// </remarks>
    protected IntegrationEvent(Guid id, DateTimeOffset occurredAtUtc)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc;
    }
}
