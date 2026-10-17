using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.AnimalFeedingPlans;

public record ExcludeAnimalFromPlanCommand(
    Guid PlanId,
    DateOnly ExclusionDate,
    string Reason,
    DateOnly? ResumesOn) : IRequest;

public class ExcludeAnimalFromPlanCommandHandler : IRequestHandler<ExcludeAnimalFromPlanCommand>
{
    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly ILogger<ExcludeAnimalFromPlanCommandHandler> _logger;
    private readonly Farm360.Application.Common.Interfaces.IUnitOfWork _unitOfWork;

    public ExcludeAnimalFromPlanCommandHandler(
        IAnimalFeedingPlanRepository repository,
        ILogger<ExcludeAnimalFromPlanCommandHandler> logger,
        Farm360.Application.Common.Interfaces.IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ExcludeAnimalFromPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _repository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.AnimalFeedingPlan), request.PlanId);

        plan.AddExclusion(request.ExclusionDate, request.Reason, request.ResumesOn);
        _repository.Update(plan);
        // Was never saved before, so a pause entered in the app silently did nothing.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Added exclusion to feeding plan {PlanId} for date {Date}", plan.Id, request.ExclusionDate);
    }
}
