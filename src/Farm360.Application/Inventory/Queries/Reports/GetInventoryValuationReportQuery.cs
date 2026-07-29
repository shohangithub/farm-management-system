using Farm360.Application.Inventory.DTOs;
using Farm360.Application.Inventory.Mappings;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.Reports;

public sealed record GetInventoryValuationReportQuery(Guid FarmId) : IRequest<InventoryValuationReportDto>;

public sealed class GetInventoryValuationReportQueryHandler : IRequestHandler<GetInventoryValuationReportQuery, InventoryValuationReportDto>
{
    private readonly IInventoryItemRepository _repository;

    public GetInventoryValuationReportQueryHandler(IInventoryItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<InventoryValuationReportDto> Handle(GetInventoryValuationReportQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
        var dtos = items.Select(x => x.ToDto()).ToList();

        decimal totalValue = dtos.Sum(x => x.TotalValueBdt);
        int lowStockCount = dtos.Count(x => x.Status == Domain.Inventory.Enums.InventoryStatus.LowStock);
        int outOfStockCount = dtos.Count(x => x.Status == Domain.Inventory.Enums.InventoryStatus.OutOfStock);

        return new InventoryValuationReportDto(
            request.FarmId,
            Math.Round(totalValue, 2),
            dtos.Count,
            lowStockCount,
            outOfStockCount,
            dtos.AsReadOnly());
    }
}
