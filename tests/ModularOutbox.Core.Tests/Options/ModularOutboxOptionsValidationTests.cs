using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModularOutbox.Core.Options;
using Shouldly;

namespace ModularOutbox.Core.Tests.Options;

public class ModularOutboxOptionsValidationTests
{
    [Fact]
    public void Defaults_ShouldBeCorrectlyInitialized()
    {
        // Act
        var options = new ModularOutboxOptions();

        // Assert
        options.BatchSize.ShouldBe(100);
        options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
        options.MaxRetries.ShouldBe(5);
        options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(15));
        options.CleanupAfter.ShouldBe(TimeSpan.FromHours(24));
        options.CleanupInterval.ShouldBe(TimeSpan.FromHours(12));
        options.EnableDeliveryService.ShouldBeTrue();
        options.EnableCleanupService.ShouldBeTrue();
        options.Schema.ShouldBe("messaging");
        options.ConnectionString.ShouldBeNull();
    }

    [Fact]
    public void JsonSerializerOptions_ShouldBeConfiguredWithProductionDefaults()
    {
        // Act
        var options = new ModularOutboxOptions();

        // Assert
        options.JsonSerializerOptions.ShouldNotBeNull();
        options.JsonSerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        options.JsonSerializerOptions.PropertyNameCaseInsensitive.ShouldBeTrue();
        options.JsonSerializerOptions.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
        options.JsonSerializerOptions.ReferenceHandler.ShouldBe(ReferenceHandler.IgnoreCycles);
        options.JsonSerializerOptions.Converters.ShouldContain(c => c is JsonStringEnumConverter);
    }

    [Fact]
    public void Validate_WhenAllPropertiesAreValid_PassesValidation()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        var results = ValidateModel(options);

        // Assert
        results.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Server=localhost;Database=OutboxDb;", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void ConnectionString_Validation(string? connectionString, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.ConnectionString = connectionString!;

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.ConnectionString), isValid);
    }

    [Theory]
    [InlineData("messaging", true)]
    [InlineData("outbox_schema", true)]
    [InlineData("_customSchema1", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("123schema", false)] // Starts with a digit
    [InlineData("schema-name", false)] // Hyphens not allowed in standard SQL identifier
    [InlineData("schema.name", false)] // Dots not allowed in schema name
    public void Schema_Validation(string? schema, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.Schema = schema!;

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.Schema), isValid);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5000, true)]
    [InlineData(10000, true)]
    [InlineData(0, false)]
    [InlineData(10001, false)]
    [InlineData(-10, false)]
    public void BatchSize_Validation(int batchSize, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.BatchSize = batchSize;

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.BatchSize), isValid);
    }

    [Theory]
    [InlineData("00:00:01", true)] // 1 second (Min)
    [InlineData("00:00:05", true)] // 5 seconds
    [InlineData("1.00:00:00", true)] // 1 day (Max)
    [InlineData("00:00:00.500", false)] // 500 ms (Below Min)
    [InlineData("1.00:00:01", false)] // 1 day + 1 sec (Above Max)
    public void PollingInterval_Validation(string timespanString, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.PollingInterval = TimeSpan.Parse(timespanString);

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.PollingInterval), isValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void MaxRetries_Validation(int maxRetries, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxRetries = maxRetries;

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.MaxRetries), isValid);
    }

    [Theory]
    [InlineData("00:00:15", true)] // 15 seconds (Min)
    [InlineData("00:05:00", true)] // 5 minutes
    [InlineData("00:10:00", true)] // 10 minutes (Max)
    [InlineData("00:00:14", false)] // 14 seconds (Below Min)
    [InlineData("00:10:01", false)] // 10 min 1 sec (Above Max)
    public void LockTimeout_Validation(string timespanString, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.LockTimeout = TimeSpan.Parse(timespanString);

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.LockTimeout), isValid);
    }

    [Theory]
    [InlineData("00:01:00", true)] // 1 minute (Min)
    [InlineData("24.00:00:00", true)] // 24 hours
    [InlineData("365.00:00:00", true)] // 365 days (Max)
    [InlineData("00:00:59", false)] // 59 seconds (Below Min)
    [InlineData("365.00:00:01", false)] // 365 days + 1 sec (Above Max)
    public void CleanupAfter_Validation(string timespanString, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.CleanupAfter = TimeSpan.Parse(timespanString);

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.CleanupAfter), isValid);
    }

    [Theory]
    [InlineData("00:00:10", true)] // 10 seconds (Min)
    [InlineData("01:00:00", true)] // 1 hour
    [InlineData("1.00:00:00", true)] // 1 day (Max)
    [InlineData("00:00:09", false)] // 9 seconds (Below Min)
    [InlineData("1.00:00:01", false)] // 1 day + 1 sec (Above Max)
    public void CleanupInterval_Validation(string timespanString, bool isValid)
    {
        // Arrange
        var options = CreateValidOptions();
        options.CleanupInterval = TimeSpan.Parse(timespanString);

        // Act
        var results = ValidateModel(options);

        // Assert
        AssertValidationResult(results, nameof(ModularOutboxOptions.CleanupInterval), isValid);
    }

    private static ModularOutboxOptions CreateValidOptions() =>
        new() { ConnectionString = "Server=localhost;Database=ModularOutbox;Integrated Security=SSPI;" };

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        Validator.TryValidateObject(model, context, validationResults, validateAllProperties: true);
        return validationResults;
    }

    private static void AssertValidationResult(
        IEnumerable<ValidationResult> results,
        string propertyName,
        bool shouldBeValid
    )
    {
        var propertyError = results.FirstOrDefault(r => r.MemberNames.Contains(propertyName));

        if (shouldBeValid)
        {
            propertyError.ShouldBeNull(
                $"Property '{propertyName}' was expected to be valid, but had error: '{propertyError?.ErrorMessage}'"
            );
        }
        else
        {
            propertyError.ShouldNotBeNull(
                $"Property '{propertyName}' was expected to produce a validation error, but passed validation."
            );
        }
    }
}
