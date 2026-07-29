using Farm360.Application.Intelligence.Interfaces;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Farm360.Infrastructure.BackgroundServices.Intelligence;

public sealed class IntelligenceEventChannel : IIntelligenceEventChannel
{
    private readonly Channel<INotification> _channel;

    public IntelligenceEventChannel()
    {
        var options = new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<INotification>(options);
    }

    public async ValueTask EnqueueEventAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(notification, cancellationToken);
    }

    public IAsyncEnumerable<INotification> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
