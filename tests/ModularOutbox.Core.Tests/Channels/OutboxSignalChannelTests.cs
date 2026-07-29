using ModularOutbox.Core.Channels;
using Shouldly;

namespace ModularOutbox.Core.Tests.Channels;

public class OutboxSignalChannelTests
{
    [Fact]
    public async Task Signal_MultipleTimes_CoalescesToOneSignal()
    {
        // Arrange
        var channel = new OutboxSignalChannel();

        // Act - Trigger multiple signals rapidly
        for (var i = 0; i < 100; i++)
        {
            channel.Signal();
        }

        // Assert - Reader should wake up once
        var hasSignal = await channel.Reader.WaitToReadAsync(TestContext.Current.CancellationToken);
        hasSignal.ShouldBeTrue();

        // Read the single coalesced signal
        channel.Reader.TryRead(out var signal).ShouldBeTrue();
        signal.ShouldBeTrue();

        // Queue should now be empty even though 100 signals were fired
        channel.Reader.TryRead(out _).ShouldBeFalse();
    }
}
