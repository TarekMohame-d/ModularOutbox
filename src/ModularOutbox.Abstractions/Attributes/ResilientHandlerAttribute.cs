namespace ModularOutbox.Abstractions.Attributes;

/// <summary>
/// Specifies the named resilience pipeline (e.g., retries, circuit breakers, timeouts, or fallbacks)
/// to execute around the decorated event handler.
/// </summary>
/// <remarks>
/// <para>
/// When applied to an outbox/inbox handler class or handling method, the underlying framework routes
/// message execution through a registered resilience strategy mapped to <see cref="PipelineName"/>.
/// </para>
/// <para>
/// <b>Targeting &amp; Precedence:</b>
/// <list type="bullet">
///   <item>
///     <description>When applied at the <b>Class level</b>, the resilience pipeline applies to all handling methods within that class.</description>
///   </item>
///   <item>
///     <description>When applied at the <b>Method level</b>, it overrides any class-level <see cref="ResilientHandlerAttribute"/> specification.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// Because <see cref="AttributeUsageAttribute.Inherited"/> is set to <see langword="true"/>, derived handler classes
/// automatically inherit this resilience strategy unless explicitly overridden.
/// </para>
/// </remarks>
/// <param name="pipelineName">
/// The unique name of the resilience pipeline configured in the application's dependency injection container.
/// Defaults to <c>"ModularOutbox.Default"</c>.
/// </param>
/// <example>
/// The following example demonstrates applying a specific resilience strategy to an event handler class:
/// <code>
/// [ResilientHandler("PaymentProcessingPipeline")]
/// public class PaymentProcessedHandler : IInboxMessageHandler&lt;PaymentProcessedEvent&gt;
/// {
///     public async Task HandleAsync(PaymentProcessedEvent @event, CancellationToken cancellationToken)
///     {
///         // Execution will be wrapped with the "PaymentProcessingPipeline" strategy
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ResilientHandlerAttribute(string pipelineName = "ModularOutbox.Default") : Attribute
{
    /// <summary>
    /// Gets the registered logical name of the resilience pipeline used to wrap handler execution.
    /// </summary>
    /// <value>
    /// A string representing the pipeline identifier. Defaults to <c>"ModularOutbox.Default"</c>.
    /// </value>
    public string PipelineName { get; } = pipelineName;
}
