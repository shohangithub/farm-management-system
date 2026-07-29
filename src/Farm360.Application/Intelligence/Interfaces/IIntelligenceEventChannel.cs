using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Interfaces;

public interface IIntelligenceEventChannel
{
    ValueTask EnqueueEventAsync(INotification notification, CancellationToken cancellationToken = default);
    IAsyncEnumerable<INotification> ReadAllAsync(CancellationToken cancellationToken = default);
}
