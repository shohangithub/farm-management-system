using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
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
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public RecordMortalityCommandHandler(
        IMortalityRecordRepository repository,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(RecordMortalityCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

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
