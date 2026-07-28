using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.DTOs;
using Farm360.Application.Feeding.Mappings;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.FeedIngredients;

public sealed record GetFeedIngredientsQuery(bool IncludePreloaded = true) : IRequest<IReadOnlyList<FeedIngredientDto>>;

public sealed class GetFeedIngredientsQueryHandler : IRequestHandler<GetFeedIngredientsQuery, IReadOnlyList<FeedIngredientDto>>
{
    private readonly IFeedIngredientRepository _repository;
    private readonly ITenantService _tenantService;

    public GetFeedIngredientsQueryHandler(IFeedIngredientRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<FeedIngredientDto>> Handle(GetFeedIngredientsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(_tenantService.TenantId, request.IncludePreloaded, cancellationToken);
        return items.Select(i => i.ToDto()).ToList();
    }
}
