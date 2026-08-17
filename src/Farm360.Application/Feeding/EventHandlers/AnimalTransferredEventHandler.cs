using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class AnimalTransferredEventHandler : INotificationHandler<AnimalTransferredEvent>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly ILogger<AnimalTransferredEventHandler> _logger;

    public AnimalTransferredEventHandler(IAnimalFeedingPlanRepository planRepository, ILogger<AnimalTransferredEventHandler> logger)
    {
        _planRepository = planRepository;
        _logger = logger;
    }

    public async Task Handle(AnimalTransferredEvent notification, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Processing AnimalTransferredEvent for AnimalId: {AnimalId}. Updating active feeding plans location.", notification.AnimalId);

        var activePlan = await _planRepository.GetActivePlanForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken);
        if (activePlan != null)
        {
            activePlan.UpdateLocation(notification.ToShedId, null);
            _planRepository.Update(activePlan);
            
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Updated active feeding plan {PlanId} location for transferred animal {AnimalId}", activePlan.Id, notification.AnimalId);
        }
    }
}
