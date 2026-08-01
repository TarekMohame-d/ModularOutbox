namespace ModularOutbox.Abstractions.Attributes;

/// <summary>
/// Assigns a persistent, logical contract name to an outbox event class to decouple message identification
/// from C# type names and namespaces.
/// </summary>
/// <remarks>
/// <para>
/// By default, in-memory outbox/inbox systems often fallback to using fully-qualified C# type names
/// (e.g., <c>MyCompany.Ordering.Events.OrderCreated</c>) for routing and message resolution.
/// If you rename, move, or refactor the underlying class, existing serialized messages or in-flight events
/// may fail to resolve or deserialize properly.
/// </para>
/// <para>
/// Decorating your event classes with <see cref="OutboxMessageNameAttribute"/> establishes an explicit,
/// immutable name (e.g., <c>"orders.order-created.v1"</c>), guaranteeing backward compatibility and refactoring safety.
/// </para>
/// </remarks>
/// <example>
/// The following example shows how to apply a persistent logical name to an outbox event contract:
/// <code>
/// [OutboxMessageName("orders.order-created.v1")]
/// public sealed record OrderCreatedEvent(Guid OrderId, decimal TotalAmount) : IOutboxMessage;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class OutboxMessageNameAttribute : Attribute
{
    /// <summary>
    /// Gets the persistent logical name assigned to the outbox message.
    /// </summary>
    /// <value>
    /// A non-empty, non-whitespace string representing the explicit contract identifier of the event.
    /// </value>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxMessageNameAttribute"/> class with a specified logical event name.
    /// </summary>
    /// <param name="name">
    /// The explicit logical identifier for the event (e.g., <c>"ordering.order-created"</c> or <c>"v1.payment-processed"</c>).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or consists only of whitespace characters.
    /// </exception>
    public OutboxMessageNameAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Outbox message name cannot be empty.", nameof(name));

        Name = name;
    }
}
