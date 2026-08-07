using FluentValidation;

namespace Farm360.Application.Inventory.Commands.PurchaseOrders;

public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(v => v.FarmId).NotEmpty();
        RuleFor(v => v.SupplierId).NotEmpty();
        RuleFor(v => v.OrderDate).NotEmpty();
        
        RuleFor(v => v.Items)
            .NotEmpty().WithMessage("Purchase order must contain at least one item.");

        RuleForEach(v => v.Items).SetValidator(new PurchaseOrderItemDtoValidator());
    }
}

public class PurchaseOrderItemDtoValidator : AbstractValidator<PurchaseOrderItemDto>
{
    public PurchaseOrderItemDtoValidator()
    {
        RuleFor(v => v.InventoryItemId).NotEmpty();
        RuleFor(v => v.Quantity).GreaterThan(0);
        RuleFor(v => v.UnitCostBdt).GreaterThanOrEqualTo(0);
    }
}
