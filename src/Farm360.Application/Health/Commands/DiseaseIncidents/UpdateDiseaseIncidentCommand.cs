using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.DiseaseIncidents;

public sealed record UpdateDiseaseIncidentCommand(
    Guid IncidentId,
    IncidentStatus Status,
    int AffectedAnimalCount,
    string? Notes
) : IRequest;

public sealed class UpdateDiseaseIncidentCommandValidator : AbstractValidator<UpdateDiseaseIncidentCommand>
{
    public UpdateDiseaseIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.AffectedAnimalCount).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateDiseaseIncidentCommandHandler : IRequestHandler<UpdateDiseaseIncidentCommand>
{
    private readonly IDiseaseIncidentRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDiseaseIncidentCommandHandler(
        IDiseaseIncidentRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateDiseaseIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken);

        if (incident == null || incident.TenantId != _tenantService.TenantId)
            throw new KeyNotFoundException("Incident not found.");

        incident.UpdateStatus(request.Status, request.Notes);
        incident.UpdateAffectedCount(request.AffectedAnimalCount);

        _repository.Update(incident);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
