using Farm360.Application.Common.Models;
using MediatR;

namespace Farm360.Application.Inventory.Queries.GetInventoryMovementReport;

public record GetInventoryMovementReportQuery(Guid FarmId, DateOnly StartDate, DateOnly EndDate) 
    : IRequest<InventoryMovementReportDto>;

public record InventoryMovementReportDto(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<InventoryMovementItemDto> Items);

public record InventoryMovementItemDto(
    Guid ItemId,
    string ItemName,
    string Category,
    string UnitOfMeasure,
    decimal OpeningStock,
    decimal QuantityReceived,
    decimal QuantityConsumed,
    decimal QuantityWrittenOff,
    decimal ClosingStock,
    decimal CurrentValuationBdt);
