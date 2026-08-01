namespace ModularOutbox.Abstractions;

/// <summary>
/// Defines a contract for asynchronously processing a specific <see cref="IIntegrationEvent"/>.
/// </summary>
/// <typeparam name="TEvent">
/// The specific type of <see cref="IIntegrationEvent"/> this handler processes.
/// This type parameter is contravariant (<see langword="in"/>), allowing handlers to process base event implementations.
/// </typeparam>
/// <remarks>
/// <para>
/// Classes implementing this interface are discovered and registered with the dependency injection container.
/// When an inbox event of type <typeparamref name="TEvent"/> is dispatched, the framework resolves and invokes
/// the corresponding handler implementation.
/// </para>
/// <para>
/// Handlers can be decorated with <see cref="Attributes.ResilientHandlerAttribute"/> to wrap
/// their execution inside named resilience pipelines (e.g., retries, timeouts, or circuit breakers).
/// </para>
/// </remarks>
/// <example>
/// The following example demonstrates implementing an event handler for a custom integration event:
/// <code>
/// [ResilientHandler("OrdersPipeline")]
/// public sealed class OrderPlacedEventHandler : IIntegrationEventHandler&lt;OrderPlacedIntegrationEvent&gt;
/// {
///     private readonly ILogger&lt;OrderPlacedEventHandler&gt; _logger;
///
///     public OrderPlacedEventHandler(ILogger&lt;OrderPlacedEventHandler&gt; logger)
///     {
///         _logger = logger;
///     }
///
///     public async Task HandleAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct = default)
///     {
///         _logger.LogInformation("Processing order {OrderId} placed at {OccurredAt}",
///             @event.OrderId, @event.OccurredAtUtc);
///
///         // Execute domain or integration logic...
///     }
/// }
/// </code>
/// </example>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Asynchronously processes the incoming integration event.
    /// </summary>
    /// <param name="event">The event payload instance to process.</param>
    /// <param name="ct">A token to monitor for cancellation requests during handler execution.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous message handling operation.</returns>
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
