using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationProtocols;

public sealed record AssignProtocolToAnimalsCommand(
    Guid ProtocolId,
    IReadOnlyList<Guid> AnimalIds,
    DateOnly StartDate
) : IRequest;

public sealed class AssignProtocolToAnimalsCommandValidator : AbstractValidator<AssignProtocolToAnimalsCommand>
{
    public AssignProtocolToAnimalsCommandValidator()
    {
        RuleFor(x => x.ProtocolId).NotEmpty();
        RuleFor(x => x.AnimalIds).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty();
    }
}

internal sealed class AssignProtocolToAnimalsCommandHandler : IRequestHandler<AssignProtocolToAnimalsCommand>
{
    private readonly IVaccinationRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAnimalRepository? _animalRepository;

    public AssignProtocolToAnimalsCommandHandler(
        IVaccinationRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork,
        IAnimalRepository? animalRepository = null)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
        _animalRepository = animalRepository;
    }

    public async Task Handle(AssignProtocolToAnimalsCommand request, CancellationToken cancellationToken)
    {
        var protocol = await _repository.GetProtocolByIdAsync(request.ProtocolId, cancellationToken);

        if (protocol == null || protocol.TenantId != _tenantService.TenantId)
            throw new KeyNotFoundException("Protocol not found.");

        // A protocol schedules future doses: only animals still on the farm can be enrolled.
        // Doses are scheduled only for animals that exist in this tenant (unknown ids are refused).
        var animalIds = request.AnimalIds.Distinct().ToList();
        if (_animalRepository is not null)
        {
            var animals = await _animalRepository.GetByIdsAsync(animalIds, cancellationToken);
            foreach (var animal in animals)
            {
                animal.EnsureOnFarm("a vaccination protocol");
            }

            var missing = animalIds.Except(animals.Select(a => a.Id)).Count();
            if (missing > 0)
                throw new KeyNotFoundException($"{missing} of the chosen animals were not found.");
        }

        var events = new List<VaccinationEvent>();

        foreach (var animalId in animalIds)
        {
            foreach (var step in protocol.Steps)
            {
                var scheduledDate = request.StartDate.AddDays(step.TargetAgeDays);

                var @event = VaccinationEvent.Schedule(
                    _tenantService.TenantId,
                    animalId,
                    step.Id,
                    step.VaccineName,
                    string.Empty,
                    scheduledDate,
                    $"Scheduled from protocol '{protocol.Title}' - Step '{step.StepName}'",
                    // The step's vaccine stock item and dose, so giving the dose deducts the stock and books its cost.
                    step.InventoryItemId,
                    step.DosageQuantity);

                events.Add(@event);
            }
        }

        _repository.AddEvents(events);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
