using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using ModularOutbox.Core.Storage;

namespace ModularOutbox.Core.Services;

/// <summary>
/// Background service that removes old processed messages from the outbox.
/// </summary>
internal sealed class OutboxCleanupService(
    IServiceProvider serviceProvider,
    IOptions<ModularOutboxOptions> options,
    ILogger<OutboxCleanupService> logger
) : BackgroundService
{
    private readonly ModularOutboxOptions _options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox cleanup service started");

        using var timer = new PeriodicTimer(_options.CleanupInterval);

        do
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during outbox cleanup");
            }
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));

        logger.LogInformation("Outbox cleanup service stopped");
    }

    private async Task PerformCleanupAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var outboxStorage = scope.ServiceProvider.GetRequiredService<IOutboxStorage>();

        await outboxStorage.CleanupOldMessagesAsync(ct);
    }
}
