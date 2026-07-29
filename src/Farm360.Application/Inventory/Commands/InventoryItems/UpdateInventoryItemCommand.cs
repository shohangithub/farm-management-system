using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Inventory.Commands.InventoryItems;

public sealed record UpdateInventoryItemCommand(
    Guid Id,
    string Name,
    InventoryCategory Category,
    string UnitOfMeasure,
    decimal ReorderThreshold,
    string? StorageLocation = null,
    bool IsActive = true) : IRequest;

public sealed class UpdateInventoryItemCommandValidator : AbstractValidator<UpdateInventoryItemCommand>
{
    public UpdateInventoryItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(30);
        RuleFor(x => x.ReorderThreshold).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateInventoryItemCommandHandler : IRequestHandler<UpdateInventoryItemCommand>
{
    private readonly IInventoryItemRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateInventoryItemCommandHandler(IInventoryItemRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Inventory item with ID '{request.Id}' was not found.");

        item.UpdateDetails(request.Name, request.Category, request.UnitOfMeasure, request.ReorderThreshold, request.StorageLocation);
        item.SetActiveStatus(request.IsActive);

        _repository.Update(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
