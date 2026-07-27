using Farm360.Application.Health.DTOs;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.DiseaseIncidents;

public sealed record GetDiseaseIncidentDetailQuery(Guid Id) : IRequest<DiseaseIncidentDetailDto?>;

internal sealed class GetDiseaseIncidentDetailQueryHandler(IDiseaseIncidentRepository repository)
    : IRequestHandler<GetDiseaseIncidentDetailQuery, DiseaseIncidentDetailDto?>
{
    public async Task<DiseaseIncidentDetailDto?> Handle(GetDiseaseIncidentDetailQuery request, CancellationToken cancellationToken)
    {
        var incident = await repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (incident == null)
        {
            return null;
        }

        return new DiseaseIncidentDetailDto(
            incident.Id,
            incident.FarmId,
            incident.ShedId,
            incident.DiseaseName,
            incident.Severity,
            incident.IncidentDate,
            incident.Symptoms,
            incident.AffectedAnimalCount,
            incident.Status,
            incident.Notes,
            incident.AffectedAnimalIds.ToList());
    }
}
