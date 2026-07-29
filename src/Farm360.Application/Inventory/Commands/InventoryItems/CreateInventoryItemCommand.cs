using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
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
    string? StorageLocation = null) : IRequest<Guid>;

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

    public CreateInventoryItemCommandHandler(
        IInventoryItemRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
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

        await _repository.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
