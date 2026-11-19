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

/// <summary>
/// A queued intelligence message that belongs to one tenant. The background service sets this
/// tenant on the processing scope (EF's tenant filter), as a request or a job would.
/// </summary>
public interface ITenantScopedNotification : INotification
{
    System.Guid TenantId { get; }
}
