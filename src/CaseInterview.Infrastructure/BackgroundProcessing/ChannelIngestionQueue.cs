using System.Threading.Channels;
using CaseInterview.Application.Common.Interfaces;

namespace CaseInterview.Infrastructure.BackgroundProcessing;

public class ChannelIngestionQueue : IIngestionQueue
{
    private readonly Channel<IngestionTask> _channel;

    public ChannelIngestionQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<IngestionTask>(options);
    }

    public async ValueTask EnqueueAsync(IngestionTask task, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(task, cancellationToken);
    }

    public async ValueTask<IngestionTask> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }
}
