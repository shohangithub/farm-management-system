using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.AnimalFeedingPlans;

public record CancelFeedingPlanCommand(Guid Id) : IRequest;

public class CancelFeedingPlanCommandHandler : IRequestHandler<CancelFeedingPlanCommand>
{
    private readonly IFarmClock _clock;

    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelFeedingPlanCommandHandler> _logger;

    public CancelFeedingPlanCommandHandler(
        IAnimalFeedingPlanRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CancelFeedingPlanCommandHandler> logger,
        IFarmClock? clock = null)
    {
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(CancelFeedingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.AnimalFeedingPlan), request.Id);

        plan.Cancel(_clock.Today);
        _repository.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Cancelled feeding plan {PlanId}", plan.Id);
    }
}
