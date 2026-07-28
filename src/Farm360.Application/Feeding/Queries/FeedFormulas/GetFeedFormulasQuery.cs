using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Feeding.DTOs;
using Farm360.Application.Feeding.Mappings;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.FeedFormulas;

public sealed record GetFeedFormulasQuery(int PageNumber = 1, int PageSize = 10, string? SearchTerm = null) : IRequest<PagedResult<FeedFormulaDto>>;

public sealed class GetFeedFormulasQueryHandler : IRequestHandler<GetFeedFormulasQuery, PagedResult<FeedFormulaDto>>
{
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _ingredientRepository;
    private readonly ITenantService _tenantService;

    public GetFeedFormulasQueryHandler(
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository ingredientRepository,
        ITenantService tenantService)
    {
        _formulaRepository = formulaRepository;
        _ingredientRepository = ingredientRepository;
        _tenantService = tenantService;
    }

    public async Task<PagedResult<FeedFormulaDto>> Handle(GetFeedFormulasQuery request, CancellationToken cancellationToken)
    {
        var items = await _formulaRepository.GetListAsync(_tenantService.TenantId, request.PageNumber, request.PageSize, request.SearchTerm, cancellationToken);
        var totalCount = await _formulaRepository.GetCountAsync(_tenantService.TenantId, request.SearchTerm, cancellationToken);

        var ingredients = await _ingredientRepository.GetAllAsync(_tenantService.TenantId, true, cancellationToken);
        var ingDict = ingredients.ToDictionary(i => i.Id, i => i.Name);

        var dtos = items.Select(f => f.ToDto(ingDict)).ToList();
        return new PagedResult<FeedFormulaDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

public sealed record GetFeedFormulaDetailQuery(Guid Id) : IRequest<FeedFormulaDto?>;

public sealed class GetFeedFormulaDetailQueryHandler : IRequestHandler<GetFeedFormulaDetailQuery, FeedFormulaDto?>
{
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _ingredientRepository;
    private readonly ITenantService _tenantService;

    public GetFeedFormulaDetailQueryHandler(
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository ingredientRepository,
        ITenantService tenantService)
    {
        _formulaRepository = formulaRepository;
        _ingredientRepository = ingredientRepository;
        _tenantService = tenantService;
    }

    public async Task<FeedFormulaDto?> Handle(GetFeedFormulaDetailQuery request, CancellationToken cancellationToken)
    {
        var formula = await _formulaRepository.GetByIdAsync(request.Id, cancellationToken);
        if (formula is null) return null;

        var ingredients = await _ingredientRepository.GetAllAsync(_tenantService.TenantId, true, cancellationToken);
        var ingDict = ingredients.ToDictionary(i => i.Id, i => i.Name);

        return formula.ToDto(ingDict);
    }
}
