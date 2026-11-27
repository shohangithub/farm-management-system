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
    private readonly IFarmBusinessRulesProvider? _rules;

    public AssignProtocolToAnimalsCommandHandler(
        IVaccinationRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork,
        IAnimalRepository? animalRepository = null,
        IFarmBusinessRulesProvider? rules = null)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
        _animalRepository = animalRepository;
        _rules = rules;
    }

    public async Task Handle(AssignProtocolToAnimalsCommand request, CancellationToken cancellationToken)
    {
        var protocol = await _repository.GetProtocolByIdAsync(request.ProtocolId, cancellationToken);

        if (protocol == null || protocol.TenantId != _tenantService.TenantId)
            throw new KeyNotFoundException("Protocol not found.");

        // A protocol schedules future doses: only animals still on the farm can be enrolled.
        // Doses are scheduled only for animals that exist in this tenant (unknown ids are refused).
        var animalIds = request.AnimalIds.Distinct().ToList();
        var weights = new Dictionary<Guid, Farm360.Application.Health.Tasks.DoseWeight>();
        if (_animalRepository is not null)
        {
            var animals = await _animalRepository.GetByIdsAsync(animalIds, cancellationToken);
            foreach (var animal in animals)
            {
                var fallback = _rules?.For(animal.TenantId, animal.FarmId).FallbackAnimalWeightKg ?? 150m;
                weights[animal.Id] = Farm360.Application.Health.Tasks.DoseWeight.For(animal.LatestWeightKg, fallback);
            }

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
            // Each step keeps its stock item and dose (so giving it deducts stock and books the cost),
            // its kind and repeat, and works a weight-based dose out from the animal's latest weight.
            var weight = weights.TryGetValue(animalId, out var w)
                ? w
                : Farm360.Application.Health.Tasks.DoseWeight.For(null, _rules?.Current.FallbackAnimalWeightKg ?? 150m);
            events.AddRange(Farm360.Application.Health.Tasks.HealthTaskScheduling.BuildEvents(
                protocol, _tenantService.TenantId, animalId, request.StartDate, weight));
        }

        _repository.AddEvents(events);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
