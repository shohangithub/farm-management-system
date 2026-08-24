using FluentValidation;

namespace Farm360.Application.Inventory.Commands.RecordStockWriteOff;

public class RecordStockWriteOffCommandValidator : AbstractValidator<RecordStockWriteOffCommand>
{
    public RecordStockWriteOffCommandValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty().WithMessage("Farm ID is required.");

        RuleFor(x => x.InventoryItemId)
            .NotEmpty().WithMessage("Inventory Item ID is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Write-off quantity must be greater than zero.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(100).WithMessage("Reason cannot exceed 100 characters.");

        RuleFor(x => x.TransactionDate)
            .NotEmpty().WithMessage("Transaction date is required.");
    }
}
