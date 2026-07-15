using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Queries.VaccinationEvents;

public sealed record GetUpcomingVaccinationsQuery(
    Guid FarmId,
    DateOnly BeforeDate
) : IRequest<IReadOnlyList<VaccinationEventDto>>;

public sealed class GetUpcomingVaccinationsQueryValidator : AbstractValidator<GetUpcomingVaccinationsQuery>
{
    public GetUpcomingVaccinationsQueryValidator()
    {
        RuleFor(q => q.FarmId).NotEmpty();
    }
}

internal sealed class GetUpcomingVaccinationsQueryHandler(
    IVaccinationRepository vaccinationRepository) : IRequestHandler<GetUpcomingVaccinationsQuery, IReadOnlyList<VaccinationEventDto>>
{
    public async Task<IReadOnlyList<VaccinationEventDto>> Handle(GetUpcomingVaccinationsQuery request, CancellationToken cancellationToken)
    {
        var events = await vaccinationRepository.GetUpcomingEventsAsync(request.FarmId, request.BeforeDate, cancellationToken);
        
        return events.Select(e => e.ToDto()).ToList();
    }
}
