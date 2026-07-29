using Farm360.Application.Intelligence.Interfaces;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Livestock.Events;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.EventHandlers;

public sealed record AnimalRegisteredNotification(AnimalRegisteredEvent DomainEvent) : INotification;
public sealed record WeightRecordedNotification(WeightRecordedEvent DomainEvent) : INotification;
// FeedConsumptionLoggedNotification is already defined in Inventory module. We can listen to it.

public sealed class IntelligenceEventDispatcher :
    INotificationHandler<AnimalRegisteredNotification>,
    INotificationHandler<WeightRecordedNotification>,
    INotificationHandler<FeedConsumptionLoggedNotification>
{
    private readonly IIntelligenceEventChannel _channel;

    public IntelligenceEventDispatcher(IIntelligenceEventChannel channel)
    {
        _channel = channel;
    }

    public async Task Handle(AnimalRegisteredNotification notification, CancellationToken cancellationToken)
    {
        await _channel.EnqueueEventAsync(notification, cancellationToken);
    }

    public async Task Handle(WeightRecordedNotification notification, CancellationToken cancellationToken)
    {
        await _channel.EnqueueEventAsync(notification, cancellationToken);
    }

    public async Task Handle(FeedConsumptionLoggedNotification notification, CancellationToken cancellationToken)
    {
        await _channel.EnqueueEventAsync(notification, cancellationToken);
    }
}
