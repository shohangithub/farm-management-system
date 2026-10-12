using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class AnimalDiedEventHandler : INotificationHandler<AnimalDiedEvent>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly ILogger<AnimalDiedEventHandler> _logger;
    private readonly Farm360.Application.Common.Interfaces.IUnitOfWork _unitOfWork;

    public AnimalDiedEventHandler(
        IAnimalFeedingPlanRepository planRepository,
        ILogger<AnimalDiedEventHandler> logger,
        Farm360.Application.Common.Interfaces.IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AnimalDiedEvent notification, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Processing AnimalDiedEvent for AnimalId: {AnimalId}. Cancelling any active feeding plans.", notification.AnimalId);

        var activePlans = await _planRepository.GetActivePlansForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken);
        if (activePlans.Count > 0)
        {
            foreach (var activePlan in activePlans)
            {
                activePlan.Cancel();
                _planRepository.Update(activePlan);
                
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Cancelled active feeding plan {PlanId} for dead animal {AnimalId}", activePlan.Id, notification.AnimalId);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
