using System.Threading.Channels;

namespace ModularOutbox.Core.Services;

/// <summary>
/// Lightweight channel-based notification mechanism to wake up the delivery service when new messages are enqueued.
/// Coalesces multiple rapid notifications into a single signal without throwing exceptions or allocating memory.
/// </summary>
internal sealed class OutboxNotification
{
    private readonly Channel<byte> _channel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        }
    );

    /// <summary>
    /// Signals that a new message was enqueued.
    /// Non-blocking, thread-safe, and zero-allocation.
    /// </summary>
    public void NotifyNewMessage()
    {
        // TryWrite instantly writes or replaces the signal without throwing exceptions.
        _channel.Writer.TryWrite(default);
    }

    /// <summary>
    /// Flushes any pending unconsumed signals from the channel.
    /// </summary>
    public void Clear()
    {
        while (_channel.Reader.TryRead(out _)) { }
    }

    /// <summary>
    /// Waits for a notification signal or until the timeout period elapses.
    /// </summary>
    /// <param name="timeout">Maximum time to wait before falling back to periodic polling.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if signaled by a new message; <c>false</c> if timed out.</returns>
    public async ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            if (await _channel.Reader.WaitToReadAsync(cts.Token).ConfigureAwait(false))
            {
                // Clear out the pending signal byte so the channel remains empty for the next cycle
                Clear();
                return true;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout elapsed—returning false allows the worker background loop to proceed with normal polling
        }

        return false;
    }
}
