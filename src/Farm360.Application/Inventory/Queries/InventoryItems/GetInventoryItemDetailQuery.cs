using Farm360.Application.Inventory.DTOs;
using Farm360.Application.Inventory.Mappings;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.InventoryItems;

public sealed record GetInventoryItemDetailQuery(Guid Id) : IRequest<InventoryItemDto>;

public sealed class GetInventoryItemDetailQueryHandler : IRequestHandler<GetInventoryItemDetailQuery, InventoryItemDto>
{
    private readonly IInventoryItemRepository _repository;

    public GetInventoryItemDetailQueryHandler(IInventoryItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<InventoryItemDto> Handle(GetInventoryItemDetailQuery request, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item with ID '{request.Id}' was not found.");

        return item.ToDto();
    }
}
