using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.Deworming;

public sealed record GetDewormingCalendarQuery(
    Guid FarmId,
    int PageNumber = 1, 
    int PageSize = 10
) : IRequest<PagedResult<DewormingCalendarEventDto>>;

internal sealed class GetDewormingCalendarQueryHandler(IVaccinationRepository repository)
    : IRequestHandler<GetDewormingCalendarQuery, PagedResult<DewormingCalendarEventDto>>
{
    public async Task<PagedResult<DewormingCalendarEventDto>> Handle(GetDewormingCalendarQuery request, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.GetDewormingEventsAsync(
            request.FarmId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(x => new DewormingCalendarEventDto(
            x.Event.Id,
            x.Event.AnimalId,
            x.AnimalTag,
            x.Event.VaccineName,
            x.Event.ScheduledDate,
            x.Event.Status
        )).ToList();

        return new PagedResult<DewormingCalendarEventDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
