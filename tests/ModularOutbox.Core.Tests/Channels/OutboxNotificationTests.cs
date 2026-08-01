using ModularOutbox.Core.Services;
using Shouldly;

namespace ModularOutbox.Core.Tests.Channels;

public class OutboxNotificationTests
{
    private readonly OutboxNotification _sut = new();

    [Fact]
    public async Task WaitAsync_WhenSignaledBeforeWait_ReturnsTrue()
    {
        // Arrange
        _sut.NotifyNewMessage();

        // Act
        var result = await _sut.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitAsync_WhenSignaledWhileWaiting_ReturnsTrue()
    {
        // Act - Start waiting asynchronously
        var waitTask = _sut.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

        await Task.Delay(50, TestContext.Current.CancellationToken); // Small delay to ensure WaitAsync is actively listening
        _sut.NotifyNewMessage();

        var result = await waitTask;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitAsync_WhenTimeoutElapsesWithoutSignal_ReturnsFalse()
    {
        // Act
        var result = await _sut.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task WaitAsync_ClearsSignalAfterConsuming_SubsequentWaitTimesOut()
    {
        // Arrange
        _sut.NotifyNewMessage();

        // Act 1: Consume the first signal
        var firstWait = await _sut.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        // Act 2: Wait again without sending a new signal
        var secondWait = await _sut.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Assert
        firstWait.ShouldBeTrue();
        secondWait.ShouldBeFalse();
    }

    [Fact]
    public async Task NotifyNewMessage_MultipleCalls_CoalescesIntoSingleSignal()
    {
        // Arrange - Fire multiple rapid notifications
        _sut.NotifyNewMessage();
        _sut.NotifyNewMessage();
        _sut.NotifyNewMessage();

        // Act
        var firstWait = await _sut.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        var secondWait = await _sut.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Assert: First wait consumes the single coalesced signal; second wait times out
        firstWait.ShouldBeTrue();
        secondWait.ShouldBeFalse();
    }

    [Fact]
    public async Task Clear_FlushesPendingSignal()
    {
        // Arrange
        _sut.NotifyNewMessage();
        _sut.Clear();

        // Act
        var result = await _sut.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task WaitAsync_WhenExternalTokenCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _sut.WaitAsync(TimeSpan.FromSeconds(5), cts.Token)
        );
    }

    [Fact]
    public async Task WaitAsync_WhenExternalTokenCanceledDuringWait_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var waitTask = _sut.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

        // Act
        cts.Cancel();

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => await waitTask);
    }
}
