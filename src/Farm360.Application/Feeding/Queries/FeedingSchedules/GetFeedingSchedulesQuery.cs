using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.DTOs;
using Farm360.Application.Feeding.Mappings;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.FeedingSchedules;

public sealed record GetFeedingSchedulesQuery(Guid FarmId) : IRequest<IReadOnlyList<FeedingScheduleDto>>;

public sealed class GetFeedingSchedulesQueryHandler : IRequestHandler<GetFeedingSchedulesQuery, IReadOnlyList<FeedingScheduleDto>>
{
    private readonly IFeedingScheduleRepository _scheduleRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly ITenantService _tenantService;

    public GetFeedingSchedulesQueryHandler(
        IFeedingScheduleRepository scheduleRepository,
        IFeedFormulaRepository formulaRepository,
        ITenantService tenantService)
    {
        _scheduleRepository = scheduleRepository;
        _formulaRepository = formulaRepository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<FeedingScheduleDto>> Handle(GetFeedingSchedulesQuery request, CancellationToken cancellationToken)
    {
        var schedules = await _scheduleRepository.GetListByFarmAsync(_tenantService.TenantId, request.FarmId, cancellationToken);
        if (schedules.Count == 0) return Array.Empty<FeedingScheduleDto>();

        var formulas = await _formulaRepository.GetListAsync(_tenantService.TenantId, 1, 100, null, cancellationToken);
        var formulaDict = formulas.ToDictionary(f => f.Id, f => f.Title);

        return schedules.Select(s => s.ToDto(
            formulaDict.TryGetValue(s.FormulaId, out var title) ? title : "Formula"
        )).ToList();
    }
}
