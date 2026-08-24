using Farm360.Application.Common.Models;
using MediatR;

namespace Farm360.Application.Inventory.Queries.GetExpiringItems;

public record GetExpiringItemsQuery(Guid FarmId, int DaysThreshold = 30) : IRequest<List<ExpiringItemDto>>;

public record ExpiringItemDto(
    Guid ItemId,
    string ItemName,
    string Category,
    string UnitOfMeasure,
    string? BatchNumber,
    DateOnly ExpiryDate,
    int DaysUntilExpiry);
