using System.Threading.Channels;

namespace ModularOutbox.Core.Channels;

public sealed class OutboxSignalChannel
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        }
    );

    public ChannelReader<bool> Reader => _channel.Reader;

    public void Signal()
    {
        _channel.Writer.TryWrite(true);
    }
}
