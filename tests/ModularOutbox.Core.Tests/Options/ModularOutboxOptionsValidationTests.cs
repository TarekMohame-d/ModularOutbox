using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.DependencyInjection;
using ModularOutbox.Core.Options;
using Shouldly;

namespace ModularOutbox.Core.Tests.Options;

public class ModularOutboxOptionsValidationTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10001)]
    public void ConfigureOptions_InvalidBatchSize_ThrowsOptionsValidationException(int invalidBatchSize)
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ModularOutboxBuilder(services);

        builder.ConfigureOptions(opts =>
        {
            opts.BatchSize = invalidBatchSize;
        });

        var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ModularOutboxOptions>>().Value
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("schema with spaces")]
    [InlineData("schema-with-hyphens")]
    [InlineData("123invalid_start")]
    public void ConfigureOptions_InvalidSchema_ThrowsOptionsValidationException(string invalidSchema)
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ModularOutboxBuilder(services);

        builder.ConfigureOptions(opts =>
        {
            opts.Schema = invalidSchema;
        });

        var provider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ModularOutboxOptions>>().Value
        );
    }
}
