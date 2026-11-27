using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Queries.SpecializedReports;

public sealed record AnimalHealthReportDto(
    IReadOnlyList<VaccinationEventDto> Vaccinations,
    IReadOnlyList<MedicalTreatmentDto> Treatments,
    IReadOnlyList<DiseaseIncidentDto> DiseaseIncidents
);

public sealed record GetAnimalHealthReportQuery(Guid AnimalId) : IRequest<AnimalHealthReportDto>;

public sealed class GetAnimalHealthReportQueryValidator : AbstractValidator<GetAnimalHealthReportQuery>
{
    public GetAnimalHealthReportQueryValidator()
    {
        RuleFor(q => q.AnimalId).NotEmpty();
    }
}

internal sealed class GetAnimalHealthReportQueryHandler(
    IVaccinationRepository vaccinationRepository,
    IMedicalTreatmentRepository medicalTreatmentRepository,
    IDiseaseIncidentRepository diseaseIncidentRepository,
    IAnimalTagNames? tags = null) : IRequestHandler<GetAnimalHealthReportQuery, AnimalHealthReportDto>
{
    public async Task<AnimalHealthReportDto> Handle(GetAnimalHealthReportQuery request, CancellationToken cancellationToken)
    {
        var vaccinations = await vaccinationRepository.GetEventsByAnimalIdAsync(request.AnimalId, cancellationToken);
        var treatments = await medicalTreatmentRepository.GetByAnimalIdAsync(request.AnimalId, cancellationToken);
        var tag = (await tags.TagsForAsync([request.AnimalId], cancellationToken)).GetValueOrDefault(request.AnimalId);
        var incidents = await diseaseIncidentRepository.GetIncidentsByAnimalIdAsync(request.AnimalId, cancellationToken);

        return new AnimalHealthReportDto(
            vaccinations.Select(v => v.ToDto(tag)).ToList(),
            treatments.Select(t => t.ToDto(tag)).ToList(),
            incidents.Select(i => i.ToDto()).ToList()
        );
    }
}
