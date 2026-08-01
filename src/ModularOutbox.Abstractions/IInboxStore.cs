namespace ModularOutbox.Abstractions;

/// <summary>
/// Defines the persistence contract for tracking inbox message handling history
/// to guarantee idempotent message execution across multiple consumers.
/// </summary>
/// <remarks>
/// <para>
/// In an at-least-once message delivery model, duplicate messages can be delivered to consumers.
/// Implementations of <see cref="IInboxStore"/> persist execution records using a composite identity
/// formed by the <c>messageId</c> and <c>consumerName</c>.
/// </para>
/// <para>
/// This composite key ensures that different consumers (or bounded contexts) can independently process
/// the same event without interfering with each other's idempotency state.
/// </para>
/// </remarks>
/// <example>
/// The following example illustrates how an inbox processor checks and marks message processing:
/// <code>
/// public async Task ProcessInboxMessageAsync(Guid messageId, string consumerName, CancellationToken ct)
/// {
///     if (await _inboxStore.HasBeenProcessedAsync(messageId, consumerName, ct))
///     {
///         // Skip duplicate delivery
///         return;
///     }
///
///     // Execute business logic...
///
///     await _inboxStore.MarkAsProcessedAsync(messageId, consumerName, ct);
/// }
/// </code>
/// </example>
public interface IInboxStore
{
    /// <summary>
    /// Checks whether a specific message has already been successfully processed by a given consumer.
    /// </summary>
    /// <param name="messageId">The unique identifier of the inbox event or message.</param>
    /// <param name="consumerName">
    /// The unique logical name of the consumer or service handling the message
    /// (e.g., <c>"OrderService.EmailNotifier"</c>).
    /// </param>
    /// <param name="ct">A token to monitor for cancellation requests during the database operation.</param>
    /// <returns>
    /// <see langword="true"/> if the message has already been marked as processed for this consumer;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> HasBeenProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default);

    /// <summary>
    /// Marks a message as processed for a specific consumer, persisting the completion state
    /// to prevent future duplicate execution.
    /// </summary>
    /// <param name="messageId">The unique identifier of the inbox event or message.</param>
    /// <param name="consumerName">
    /// The unique logical name of the consumer or service handling the message.
    /// </param>
    /// <param name="ct">A token to monitor for cancellation requests during the database operation.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous storage operation.</returns>
    Task MarkAsProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default);
}
