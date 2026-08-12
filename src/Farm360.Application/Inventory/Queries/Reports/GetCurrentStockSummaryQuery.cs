using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.Reports;

public sealed record GetCurrentStockSummaryQuery(Guid FarmId) : IRequest<CurrentStockSummaryDto>;

public sealed class GetCurrentStockSummaryQueryHandler : IRequestHandler<GetCurrentStockSummaryQuery, CurrentStockSummaryDto>
{
    private readonly IInventoryItemRepository _repository;

    public GetCurrentStockSummaryQueryHandler(IInventoryItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<CurrentStockSummaryDto> Handle(GetCurrentStockSummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _repository.GetSummaryAsync(request.FarmId, cancellationToken);
        
        return new CurrentStockSummaryDto(
            summary.TotalItems,
            summary.TotalValueBdt,
            summary.LowStockCount,
            summary.OutOfStockCount);
    }
}
