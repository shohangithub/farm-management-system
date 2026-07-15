using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.DiseaseIncidents;

public sealed record ReportDiseaseIncidentCommand(
    Guid FarmId,
    Guid? ShedId,
    string DiseaseName,
    IncidentSeverity Severity,
    DateOnly IncidentDate,
    string Symptoms,
    int AffectedAnimalCount,
    string? Notes
) : IRequest<Guid>;

public sealed class ReportDiseaseIncidentCommandValidator : AbstractValidator<ReportDiseaseIncidentCommand>
{
    public ReportDiseaseIncidentCommandValidator()
    {
        RuleFor(v => v.FarmId).NotEmpty();
        RuleFor(v => v.DiseaseName).NotEmpty().MaximumLength(150);
        RuleFor(v => v.Symptoms).NotEmpty().MaximumLength(1000);
        RuleFor(v => v.AffectedAnimalCount).GreaterThan(0);
        RuleFor(v => v.Notes).MaximumLength(1000);
        RuleFor(v => v.IncidentDate).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Incident date cannot be in the future.");
    }
}

internal sealed class ReportDiseaseIncidentCommandHandler(
    IDiseaseIncidentRepository diseaseIncidentRepository,
    ITenantService tenantService,
    IUnitOfWork unitOfWork) : IRequestHandler<ReportDiseaseIncidentCommand, Guid>
{
    public async Task<Guid> Handle(ReportDiseaseIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = DiseaseIncident.Report(
            tenantService.TenantId,
            request.FarmId,
            request.ShedId,
            request.DiseaseName,
            request.Severity,
            request.IncidentDate,
            request.Symptoms,
            request.AffectedAnimalCount,
            request.Notes);

        diseaseIncidentRepository.Add(incident);
        
        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await unitOfWork.CommitTransactionAsync(tx, cancellationToken);

        return incident.Id;
    }
}
