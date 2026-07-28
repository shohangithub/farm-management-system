using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.DTOs;
using Farm360.Application.Feeding.Mappings;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.ConsumptionLogs;

public sealed record GetFeedConsumptionLogsQuery(
    Guid FarmId,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<IReadOnlyList<FeedConsumptionLogDto>>;

public sealed class GetFeedConsumptionLogsQueryHandler : IRequestHandler<GetFeedConsumptionLogsQuery, IReadOnlyList<FeedConsumptionLogDto>>
{
    private readonly IFeedConsumptionLogRepository _logRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _ingredientRepository;
    private readonly ITenantService _tenantService;

    public GetFeedConsumptionLogsQueryHandler(
        IFeedConsumptionLogRepository logRepository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository ingredientRepository,
        ITenantService tenantService)
    {
        _logRepository = logRepository;
        _formulaRepository = formulaRepository;
        _ingredientRepository = ingredientRepository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<FeedConsumptionLogDto>> Handle(GetFeedConsumptionLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await _logRepository.GetLogsAsync(_tenantService.TenantId, request.FarmId, request.FromDate, request.ToDate, cancellationToken);
        if (logs.Count == 0) return Array.Empty<FeedConsumptionLogDto>();

        var formulas = await _formulaRepository.GetListAsync(_tenantService.TenantId, 1, 100, null, cancellationToken);
        var formulaDict = formulas.ToDictionary(f => f.Id, f => f.Title);

        var ingredients = await _ingredientRepository.GetAllAsync(_tenantService.TenantId, true, cancellationToken);
        var ingDict = ingredients.ToDictionary(i => i.Id, i => i.Name);

        return logs.Select(l => l.ToDto(
            formulaDict.TryGetValue(l.FormulaId, out var title) ? title : "Formula",
            ingDict
        )).ToList();
    }
}
