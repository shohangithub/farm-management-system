using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.Deworming;

public sealed record GetDewormingCalendarQuery(
    Guid FarmId,
    int PageNumber = 1, 
    int PageSize = 10
) : IRequest<PagedResult<VaccinationEventDto>>;

internal sealed class GetDewormingCalendarQueryHandler(IVaccinationRepository repository)
    : IRequestHandler<GetDewormingCalendarQuery, PagedResult<VaccinationEventDto>>
{
    public async Task<PagedResult<VaccinationEventDto>> Handle(GetDewormingCalendarQuery request, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.GetDewormingEventsAsync(
            request.FarmId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(x => x.ToDto()).ToList();
        return new PagedResult<VaccinationEventDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
