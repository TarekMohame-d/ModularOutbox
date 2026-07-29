using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Channels;
using ModularOutbox.Core.Options;

namespace ModularOutbox.Core.Processing;

public sealed class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxSignalChannel _signalChannel;
    private readonly ModularOutboxOptions _options;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly TimeSpan PollingInterval;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        OutboxSignalChannel signalChannel,
        IOptions<ModularOutboxOptions> options,
        ILogger<OutboxProcessorBackgroundService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _signalChannel = signalChannel;
        _options = options.Value;
        _logger = logger;

        PollingInterval = TimeSpan.FromSeconds(_options.PollingIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timerTask = StartPeriodicTimerAsync(stoppingToken);
        var processorTask = ProcessChannelSignalsAsync(stoppingToken);

        await Task.WhenAll(timerTask, processorTask);
    }

    private async Task StartPeriodicTimerAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            _signalChannel.Signal();
        }
    }

    private async Task ProcessChannelSignalsAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in _signalChannel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessAllPendingOutboxMessagesAsync(stoppingToken);
        }
    }

    private async Task ProcessAllPendingOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        bool hasMore;
        do
        {
            hasMore = await ProcessOutboxBatchAsync(cancellationToken);
        } while (hasMore && !cancellationToken.IsCancellationRequested);
    }

    private async Task<bool> ProcessOutboxBatchAsync(CancellationToken ct)
    {
        _logger.LogInformation("Processing outbox messages at {Timestamp}", DateTimeOffset.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
        int messagesCount = await processor.ProcessBatchAsync(ct);

        return messagesCount == _options.BatchSize;
    }
}
