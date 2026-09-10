using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Inventory.Commands.Consumables;

public sealed record UpdateConsumableUsagePlanCommand(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate = null,
    string? Description = null,
    ConsumableUsagePlanStatus? Status = null,
    IReadOnlyList<ConsumablePlanItemInput>? Items = null) : IRequest;

public sealed class UpdateConsumableUsagePlanCommandValidator : AbstractValidator<UpdateConsumableUsagePlanCommand>
{
    public UpdateConsumableUsagePlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date must be on or after start date.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.InventoryItemId).NotEmpty();
            item.RuleFor(i => i.PlannedQuantityPerDay).GreaterThan(0);
        });
    }
}

public sealed class UpdateConsumableUsagePlanCommandHandler : IRequestHandler<UpdateConsumableUsagePlanCommand>
{
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateConsumableUsagePlanCommandHandler(
        IConsumableUsagePlanRepository planRepository,
        IInventoryItemRepository inventoryItemRepository,
        IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateConsumableUsagePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetByIdAsync(request.Id, includeItems: true, cancellationToken)
            ?? throw new NotFoundException(nameof(ConsumableUsagePlan), request.Id);

        plan.UpdateDetails(request.Name, request.StartDate, request.EndDate, request.Description);

        if (request.Status.HasValue)
        {
            switch (request.Status.Value)
            {
                case ConsumableUsagePlanStatus.Active:
                    plan.Activate();
                    break;
                case ConsumableUsagePlanStatus.Paused:
                    plan.Pause();
                    break;
                case ConsumableUsagePlanStatus.Completed:
                    plan.Complete();
                    break;
            }
        }

        if (request.Items != null)
        {
            plan.ClearItems();
            foreach (var itemInput in request.Items)
            {
                var inventoryItem = await _inventoryItemRepository.GetByIdAsync(itemInput.InventoryItemId, cancellationToken)
                    ?? throw new NotFoundException(nameof(InventoryItem), itemInput.InventoryItemId);

                plan.AddItem(inventoryItem.Id, itemInput.PlannedQuantityPerDay, itemInput.Notes);
            }
        }

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteConsumableUsagePlanCommand(Guid Id) : IRequest;

public sealed class DeleteConsumableUsagePlanCommandHandler : IRequestHandler<DeleteConsumableUsagePlanCommand>
{
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteConsumableUsagePlanCommandHandler(
        IConsumableUsagePlanRepository planRepository,
        IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteConsumableUsagePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException(nameof(ConsumableUsagePlan), request.Id);

        _planRepository.Delete(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
