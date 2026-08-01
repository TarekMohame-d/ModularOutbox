using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModularOutbox.Core.Options;

/// <summary>
/// Provides configuration options for controlling outbox event processing, background polling,
/// database schema routing, JSON serialization, and message retention policies.
/// </summary>
/// <remarks>
/// <para>
/// These options can be configured programmatically or bound directly to an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
/// section (e.g., <c>"ModularOutbox"</c> inside <c>appsettings.json</c>).
/// </para>
/// <para>
/// Data annotations (such as <see cref="RangeAttribute"/> and <see cref="RequiredAttribute"/>) are applied
/// to enforce strict startup validation when paired with <c>OptionsBuilderExtensions.ValidateDataAnnotations</c>.
/// </para>
/// </remarks>
/// <example>
/// <b>appsettings.json Configuration:</b>
/// <code lang="json">
/// {
///   "ModularOutbox": {
///     "ConnectionString": "Server=localhost;Database=OrdersDb;Integrated Security=SSPI;",
///     "Schema": "messaging",
///     "BatchSize": 250,
///     "PollingInterval": "00:00:02",
///     "MaxRetries": 3,
///     "CleanupAfter": "12:00:00"
///   }
/// }
/// </code>
///
/// <b>Programmatic C# Setup:</b>
/// <code>
/// services.AddModularOutbox(options =>
/// {
///     options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
///     options.BatchSize = 200;
///     options.PollingInterval = TimeSpan.FromSeconds(2);
///     options.EnableCleanupService = true;
/// });
/// </code>
/// </example>
public sealed class ModularOutboxOptions
{
    /// <summary>
    /// Gets or sets the maximum number of unprocessed outbox messages to fetch per database query batch.
    /// </summary>
    /// <value>
    /// An integer between <c>1</c> and <c>10,000</c>. Defaults to <c>100</c>.
    /// </value>
    /// <remarks>
    /// Lower values reduce row-locking duration on the outbox table, whereas higher values improve total processing throughput
    /// under high load at the cost of higher memory consumption.
    /// </remarks>
    [Range(1, 10000, ErrorMessage = $"{nameof(BatchSize)} must be between 1 and 10,000.")]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the fallback polling interval when no new events are actively signaled or available in the store.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> between 1 second (<c>"00:00:01"</c>) and 1 day (<c>"1.00:00:00"</c>).
    /// Defaults to 10 seconds (<c>TimeSpan.FromSeconds(5)</c>).
    /// </value>
    /// <remarks>
    /// Serves as a safety-net polling fallback to pick up unprocessed or retried messages when real-time channel notifications are idle.
    /// </remarks>
    [Range(
        typeof(TimeSpan),
        "00:00:01",
        "1.00:00:00",
        ErrorMessage = $"{nameof(PollingInterval)} must be between 1 second and 1 day."
    )]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum number of retry attempts permitted for a failing message before marking it as dead-lettered.
    /// </summary>
    /// <value>
    /// An integer between <c>0</c> and <c>100</c>. Defaults to <c>5</c>.
    /// </value>
    /// <remarks>
    /// Setting this value to <c>0</c> disables retries and immediately dead-letters messages upon the first processing failure.
    /// </remarks>
    [Range(0, 100, ErrorMessage = $"{nameof(MaxRetries)} must be a non-negative number up to 100.")]
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Gets or sets how long an outbox message is leased to a delivery worker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a worker fetches a batch of messages, each message is leased for this duration by
    /// setting its lock expiration timestamp. Other workers will not process the leased messages
    /// until the lease expires.
    /// </para>
    /// <para>
    /// Choose a value comfortably longer than the expected maximum time required to publish a
    /// message. If a worker crashes or stops responding, the lease eventually expires and another
    /// worker can retry processing the message.
    /// </para>
    /// </remarks>
    /// <value>
    /// A <see cref="TimeSpan"/> between 30 seconds (<c>"00:00:30"</c>) and
    /// 1 day (<c>"1.00:00:00"</c>).
    /// Defaults to 30 seconds (<c>TimeSpan.FromSeconds(30)</c>).
    /// </value>
    [Range(
        typeof(TimeSpan),
        "00:00:15",
        "00:10:00",
        ErrorMessage = $"{nameof(LockTimeout)} must be between 15 seconds and 10 minutes."
    )]
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the retention period for processed or dead-lettered messages before they become eligible for automated deletion.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> between 1 minute (<c>"00:01:00"</c>) and 365 days (<c>"365.00:00:00"</c>).
    /// Defaults to 24 hours (<c>TimeSpan.FromHours(24)</c>).
    /// </value>
    [Range(
        typeof(TimeSpan),
        "00:01:00",
        "365.00:00:00",
        ErrorMessage = $"{nameof(CleanupAfter)} must be between 1 minute and 365 days."
    )]
    public TimeSpan CleanupAfter { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the execution frequency for the automated outbox message cleanup background service.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> between 10 seconds (<c>"00:00:10"</c>) and 1 day (<c>"1.00:00:00"</c>).
    /// Defaults to 12 hour (<c>TimeSpan.FromHours(1)</c>).
    /// </value>
    [Range(
        typeof(TimeSpan),
        "00:00:10",
        "1.00:00:00",
        ErrorMessage = $"{nameof(CleanupInterval)} must be between 10 seconds and 1 day."
    )]
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    /// Gets or sets a value indicating whether the outbox message processor background worker should run.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if delivery processing is active; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.
    /// </value>
    /// <remarks>
    /// Useful for disabling message dispatch in specific worker nodes, testing environments, or administrative instances.
    /// </remarks>
    public bool EnableDeliveryService { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the outbox table cleanup background service should run.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if automated cleanup is active; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.
    /// </value>
    public bool EnableCleanupService { get; set; } = true;

    /// <summary>
    /// Gets the custom <see cref="System.Text.Json.JsonSerializerOptions"/> instance used when serializing and deserializing
    /// outbox message payloads.
    /// </summary>
    /// <value>
    /// A non-null <see cref="System.Text.Json.JsonSerializerOptions"/> instance initialized with production defaults.
    /// </value>
    /// <remarks>
    /// <b>Default Settings:</b>
    /// <list type="bullet">
    ///   <item><description>Property naming policy: CamelCase</description></item>
    ///   <item><description>Property name matching: Case-insensitive</description></item>
    ///   <item><description>Null writing condition: <see cref="JsonIgnoreCondition.WhenWritingNull"/></description></item>
    ///   <item><description>Cycle reference strategy: <see cref="ReferenceHandler.IgnoreCycles"/></description></item>
    ///   <item><description>Converters: <see cref="JsonStringEnumConverter"/></description></item>
    /// </list>
    /// </remarks>
    [Required(ErrorMessage = $"{nameof(JsonSerializerOptions)} cannot be null.")]
    public JsonSerializerOptions JsonSerializerOptions { get; } = GetDefaultOptions();

    /// <summary>
    /// Gets the default database schema name used when no custom schema is configured.
    /// </summary>
    private static string DefaultSchema { get; } = "messaging";

    /// <summary>
    /// Gets or sets the target database schema name where outbox table artifacts reside.
    /// </summary>
    /// <value>
    /// A string matching standard SQL identifier naming rules. Defaults to <c>"messaging"</c>.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(Schema)} cannot be empty.")]
    [RegularExpression(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        ErrorMessage = $"{nameof(Schema)} must be a valid SQL identifier (alphanumeric and underscores only)."
    )]
    public string Schema { get; set; } = DefaultSchema;

    /// <summary>
    /// Gets or sets the database connection string used by the outbox store persistence engine.
    /// </summary>
    /// <value>
    /// A non-empty connection string.
    /// </value>
    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(ConnectionString)} cannot be empty.")]
    public string ConnectionString { get; set; } = null!;

    private static JsonSerializerOptions GetDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
