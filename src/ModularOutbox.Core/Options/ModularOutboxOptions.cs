using System.ComponentModel.DataAnnotations;

namespace ModularOutbox.Core.Options;

public sealed class ModularOutboxOptions
{
    [Range(1, 10000, ErrorMessage = $"{nameof(BatchSize)} must be between 1 and 10,000.")]
    public int BatchSize { get; set; } = 50;

    [Range(1, 86400, ErrorMessage = $"{nameof(PollingIntervalSeconds)} must be at least 1 second.")]
    public int PollingIntervalSeconds { get; set; } = 10;

    [Range(0, 100, ErrorMessage = $"{nameof(MaxRetries)} must be a non-negative number.")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Database schema name for outbox tables. Defaults to "messaging".
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(Schema)} cannot be empty.")]
    [RegularExpression(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        ErrorMessage = $"{nameof(Schema)} must be a valid SQL identifier (alphanumeric and underscores only)."
    )]
    public string Schema { get; set; } = "messaging";
}
