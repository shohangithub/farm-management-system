using Farm360.Application.Common.Models;
using MediatR;

namespace Farm360.Application.Inventory.Commands.RecordStockWriteOff;

public record RecordStockWriteOffCommand(
    Guid FarmId,
    Guid InventoryItemId,
    decimal Quantity,
    string Reason,
    DateOnly TransactionDate,
    string? Notes = null) : IRequest<Guid>;
