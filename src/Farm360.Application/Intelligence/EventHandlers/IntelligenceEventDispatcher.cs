using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Livestock.Events;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.EventHandlers;

/// <summary>
/// The background-intelligence message for a new weight record. It is NOT published in the request:
/// <see cref="IntelligenceEventDispatcher"/> enqueues it, and the intelligence background service
/// publishes it in its own scope (tenant set from <see cref="TenantId"/>), where
/// <see cref="IntelligenceEventHandlers"/> evaluates the rules — keeping the request fast.
/// </summary>
public sealed record WeightRecordedNotification(WeightRecordedEvent DomainEvent) : ITenantScopedNotification
{
    public System.Guid TenantId => DomainEvent.TenantId;
}

/// <summary>
/// Bridges published domain events to the intelligence pipeline. Listens to the real
/// <see cref="WeightRecordedEvent"/> (published by RecordWeightCommandHandler) and queues the
/// background message. Only events with a background handler are queued: queueing a notification
/// that request handlers also listen to (as FeedConsumptionLoggedNotification once was) would run
/// those handlers a second time in the background and re-queue itself forever.
/// </summary>
public sealed class IntelligenceEventDispatcher : INotificationHandler<WeightRecordedEvent>
{
    private readonly IIntelligenceEventChannel _channel;

    public IntelligenceEventDispatcher(IIntelligenceEventChannel channel)
    {
        _channel = channel;
    }

    public async Task Handle(WeightRecordedEvent notification, CancellationToken cancellationToken)
    {
        await _channel.EnqueueEventAsync(new WeightRecordedNotification(notification), cancellationToken);
    }
}
