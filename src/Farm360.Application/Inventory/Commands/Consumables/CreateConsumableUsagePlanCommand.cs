using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Inventory.Commands.Consumables;

public sealed record ConsumablePlanItemInput(
    Guid InventoryItemId,
    decimal PlannedQuantityPerDay,
    string? Notes = null);

public sealed record CreateConsumableUsagePlanCommand(
    Guid FarmId,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate = null,
    string? Description = null,
    IReadOnlyList<ConsumablePlanItemInput>? Items = null) : IRequest<Guid>;

public sealed class CreateConsumableUsagePlanCommandValidator : AbstractValidator<CreateConsumableUsagePlanCommand>
{
    public CreateConsumableUsagePlanCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
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

public sealed class CreateConsumableUsagePlanCommandHandler : IRequestHandler<CreateConsumableUsagePlanCommand, Guid>
{
    private readonly IConsumableUsagePlanRepository _planRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateConsumableUsagePlanCommandHandler(
        IConsumableUsagePlanRepository planRepository,
        IInventoryItemRepository inventoryItemRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateConsumableUsagePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = new ConsumableUsagePlan(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.FarmId,
            request.Name,
            request.StartDate,
            request.EndDate,
            request.Description);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemInput in request.Items)
            {
                var inventoryItem = await _inventoryItemRepository.GetByIdAsync(itemInput.InventoryItemId, cancellationToken)
                    ?? throw new NotFoundException(nameof(InventoryItem), itemInput.InventoryItemId);

                plan.AddItem(inventoryItem.Id, itemInput.PlannedQuantityPerDay, itemInput.Notes);
            }
        }

        await _planRepository.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return plan.Id;
    }
}
