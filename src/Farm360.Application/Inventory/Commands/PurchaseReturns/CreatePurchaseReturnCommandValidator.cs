using FluentValidation;

namespace Farm360.Application.Inventory.Commands.PurchaseReturns;

public sealed class CreatePurchaseReturnCommandValidator : AbstractValidator<CreatePurchaseReturnCommand>
{
    public CreatePurchaseReturnCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().WithMessage("Farm ID is required.");
        RuleFor(x => x.PurchaseOrderId).NotEmpty().WithMessage("Purchase Order ID is required.");
        RuleFor(x => x.ReturnDate).NotEmpty().WithMessage("Return date is required.");
        RuleFor(x => x.Reason).IsInEnum().WithMessage("A valid return reason must be selected.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one return item must be specified.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.PurchaseOrderItemId).NotEmpty().WithMessage("Purchase order item ID is required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Return quantity must be greater than zero.");
        });
    }
}
