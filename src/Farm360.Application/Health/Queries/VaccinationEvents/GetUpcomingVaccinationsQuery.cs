using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Application.Livestock.Queries;
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
    IVaccinationRepository vaccinationRepository,
    IAnimalTagNames? tags = null) : IRequestHandler<GetUpcomingVaccinationsQuery, IReadOnlyList<VaccinationEventDto>>
{
    public async Task<IReadOnlyList<VaccinationEventDto>> Handle(GetUpcomingVaccinationsQuery request, CancellationToken cancellationToken)
    {
        var events = await vaccinationRepository.GetUpcomingEventsAsync(request.FarmId, request.BeforeDate, cancellationToken);

        // Ear tags for the whole list in one lookup.
        var tagById = await tags.TagsForAsync(events.Select(e => e.AnimalId), cancellationToken);
        return events.Select(e => e.ToDto(tagById.GetValueOrDefault(e.AnimalId))).ToList();
    }
}
