namespace ModularOutbox.Abstractions;

/// <summary>
/// Defines the contract for staging integration events into the outbox within the current transaction or unit of work.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IOutboxWriter"/> is the primary entry point used by application services or domain event handlers
/// to schedule an event for eventual outbox publication.
/// </para>
/// <para>
/// <b>Transactional Guarantees:</b><br/>
/// Calling <see cref="Enqueue"/> buffers the event in memory attached to the active unit of work or transaction scope.
/// The event is only persisted or dispatched permanently once the surrounding database transaction or command scope succeeds,
/// guaranteeing dual-write safety (dual-write problem prevention).
/// </para>
/// </remarks>
/// <example>
/// The following example shows how a command handler uses <see cref="IOutboxWriter"/> to stage an event alongside domain changes:
/// <code>
/// public class CreateOrderCommandHandler
/// {
///     private readonly IOrderRepository _repository;
///     private readonly IOutboxWriter _outboxWriter;
///
///     public CreateOrderCommandHandler(IOrderRepository repository, IOutboxWriter outboxWriter)
///     {
///         _repository = repository;
///         _outboxWriter = outboxWriter;
///     }
///
///     public async Task HandleAsync(CreateOrderCommand command, CancellationToken ct)
///     {
///         var order = Order.Create(command.CustomerId, command.Items);
///         await _repository.AddAsync(order, ct);
///
///         // Stage the event into the outbox within the same unit of work
///         var @event = new OrderCreatedIntegrationEvent(order.Id, order.TotalAmount);
///         _outboxWriter.Enqueue(@event, ct);
///
///         // Commit changes and outbox event together
///         await _repository.UnitOfWork.SaveChangesAsync(ct);
///     }
/// }
/// </code>
/// </example>
public interface IOutboxWriter
{
    /// <summary>
    /// Enqueues an integration event to be saved to the outbox and subsequently published.
    /// </summary>
    /// <param name="event">
    /// The <see cref="IIntegrationEvent"/> instance to stage for publication. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="ct">
    /// An optional token to monitor for cancellation requests while staging the message.
    /// </param>
    void Enqueue(IIntegrationEvent @event, CancellationToken ct = default);
}
