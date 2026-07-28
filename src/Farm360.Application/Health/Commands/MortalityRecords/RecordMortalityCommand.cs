using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.MortalityRecords;

public sealed record RecordMortalityCommand(
    Guid AnimalId,
    DateOnly DeathDate,
    CauseOfDeath CauseOfDeath,
    string? DiseaseName,
    string? PostMortemNotes,
    decimal? EstimatedEconomicLossBdt,
    Guid? DiseaseIncidentId
) : IRequest<Guid>;

public sealed class RecordMortalityCommandValidator : AbstractValidator<RecordMortalityCommand>
{
    public RecordMortalityCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.DeathDate).NotEmpty();
        RuleFor(x => x.CauseOfDeath).IsInEnum();
        
        RuleFor(x => x.DiseaseName)
            .NotEmpty()
            .When(x => x.CauseOfDeath == CauseOfDeath.Disease)
            .WithMessage("DiseaseName is required when CauseOfDeath is Disease.");
    }
}

internal sealed class RecordMortalityCommandHandler : IRequestHandler<RecordMortalityCommand, Guid>
{
    private readonly IMortalityRecordRepository _repository;
    private readonly IAnimalRepository _animalRepository;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public RecordMortalityCommandHandler(
        IMortalityRecordRepository repository,
        IAnimalRepository animalRepository,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _animalRepository = animalRepository;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(RecordMortalityCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        var animal = await _animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        if (animal.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Dead ||
            await _repository.ExistsByAnimalIdAsync(request.AnimalId, cancellationToken))
        {
            throw new InvalidOperationException($"A mortality record already exists for animal '{animal.Tag.TagId}'.");
        }

        var disposalReason = request.CauseOfDeath switch
        {
            CauseOfDeath.Disease => Farm360.Domain.Livestock.Enums.DisposalReason.Disease,
            CauseOfDeath.Accident => Farm360.Domain.Livestock.Enums.DisposalReason.Accident,
            CauseOfDeath.NaturalCauses => Farm360.Domain.Livestock.Enums.DisposalReason.NaturalDeath,
            CauseOfDeath.Slaughter => Farm360.Domain.Livestock.Enums.DisposalReason.Slaughter,
            _ => Farm360.Domain.Livestock.Enums.DisposalReason.Unknown
        };

        animal.RecordDeath(disposalReason, request.DeathDate, request.PostMortemNotes);

        var record = MortalityRecord.Record(
            _tenantService.TenantId,
            request.AnimalId,
            request.DeathDate,
            request.CauseOfDeath,
            request.DiseaseName,
            request.PostMortemNotes,
            request.EstimatedEconomicLossBdt,
            request.DiseaseIncidentId,
            currentUserId);

        _repository.Add(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return record.Id;
    }
}
