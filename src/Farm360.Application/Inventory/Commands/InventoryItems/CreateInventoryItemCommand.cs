using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Inventory.Commands.InventoryItems;

public sealed record CreateInventoryItemCommand(
    Guid FarmId,
    string Name,
    InventoryCategory Category,
    string UnitOfMeasure,
    decimal ReorderThreshold,
    string? Sku = null,
    decimal InitialStock = 0,
    decimal InitialCostBdt = 0,
    string? StorageLocation = null,
    Guid? DefaultUnitId = null,
    Guid? BaseUnitId = null) : IRequest<Guid>;

public sealed class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(30);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InitialCostBdt).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateInventoryItemCommandHandler : IRequestHandler<CreateInventoryItemCommand, Guid>
{
    private readonly IInventoryItemRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockTransactionRepository _stockTransactions;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly Farm360.Application.Inventory.Units.IUnitService? _units;

    public CreateInventoryItemCommandHandler(
        IInventoryItemRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork,
        IStockTransactionRepository stockTransactions,
        ILedgerPostingService ledgerPosting,
        Farm360.Application.Inventory.Units.IUnitService? units = null)
    {
        _units = units;
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
        _stockTransactions = stockTransactions;
        _ledgerPosting = ledgerPosting;
    }

    public async Task<Guid> Handle(CreateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = new InventoryItem(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.FarmId,
            request.Name,
            request.Sku ?? string.Empty,
            request.Category,
            request.UnitOfMeasure,
            request.ReorderThreshold,
            request.InitialStock,
            request.InitialCostBdt,
            request.StorageLocation);

        if (_units is not null)
            await Farm360.Application.Inventory.Units.ItemUnitLinker.ApplyAsync(_units, item, request.BaseUnitId, request.UnitOfMeasure, request.DefaultUnitId, cancellationToken);
        else
            item.SetDefaultUnit(request.DefaultUnitId);
        await _repository.AddAsync(item, cancellationToken);

        // Opening stock: record the movement and bring its value into the books as opening
        // owner capital, otherwise stock exists physically but not in the accounts.
        if (request.InitialStock > 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var openingId = Guid.NewGuid();
            await _stockTransactions.AddAsync(new StockTransaction(
                id: openingId,
                tenantId: _tenantService.TenantId,
                farmId: request.FarmId,
                inventoryItemId: item.Id,
                transactionType: StockTransactionType.StockIn,
                quantity: request.InitialStock,
                unitCostBdt: request.InitialCostBdt,
                balanceAfter: item.CurrentStock,
                transactionDate: today,
                reason: "Opening stock"), cancellationToken);

            await _ledgerPosting.PostStockInAsync(
                item,
                request.InitialStock * request.InitialCostBdt,
                today,
                openingId,
                StockFundingSource.OwnerContribution,
                "OPENING",
                $"Opening stock - {item.Name}",
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
