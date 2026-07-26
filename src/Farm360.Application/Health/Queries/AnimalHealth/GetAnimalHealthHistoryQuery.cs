using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Queries.AnimalHealth;

public sealed record AnimalHealthHistoryDto(
    IReadOnlyList<VaccinationEventDto> Vaccinations,
    IReadOnlyList<MedicalTreatmentDto> Treatments
);

public sealed record GetAnimalHealthHistoryQuery(Guid AnimalId) : IRequest<AnimalHealthHistoryDto>;

public sealed class GetAnimalHealthHistoryQueryValidator : AbstractValidator<GetAnimalHealthHistoryQuery>
{
    public GetAnimalHealthHistoryQueryValidator()
    {
        RuleFor(q => q.AnimalId).NotEmpty();
    }
}

internal sealed class GetAnimalHealthHistoryQueryHandler(
    IVaccinationRepository vaccinationRepository,
    IMedicalTreatmentRepository medicalTreatmentRepository) : IRequestHandler<GetAnimalHealthHistoryQuery, AnimalHealthHistoryDto>
{
    public async Task<AnimalHealthHistoryDto> Handle(GetAnimalHealthHistoryQuery request, CancellationToken cancellationToken)
    {
        var vaccinations = await vaccinationRepository.GetEventsByAnimalIdAsync(request.AnimalId, cancellationToken);
        var treatments = await medicalTreatmentRepository.GetByAnimalIdAsync(request.AnimalId, cancellationToken);

        return new AnimalHealthHistoryDto(
            vaccinations.Select(v => v.ToDto()).ToList(),
            treatments.Select(t => t.ToDto()).ToList()
        );
    }
}
