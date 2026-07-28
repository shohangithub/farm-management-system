using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.ValueObjects;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedIngredients;

public sealed record UpdateFeedIngredientCommand(
    Guid Id,
    string Name,
    FeedCategory Category,
    decimal DryMatterPct,
    decimal CrudeProteinPct,
    decimal MetabolizableEnergyMjPerKg,
    decimal CrudeFiberPct,
    decimal CalciumPct,
    decimal PhosphorusPct,
    decimal UnitCostBdt,
    string Unit = "kg",
    string? Description = null) : IRequest;

public sealed class UpdateFeedIngredientCommandValidator : AbstractValidator<UpdateFeedIngredientCommand>
{
    public UpdateFeedIngredientCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DryMatterPct).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.CrudeProteinPct).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.MetabolizableEnergyMjPerKg).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCostBdt).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateFeedIngredientCommandHandler : IRequestHandler<UpdateFeedIngredientCommand>
{
    private readonly IFeedIngredientRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFeedIngredientCommandHandler(
        IFeedIngredientRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateFeedIngredientCommand request, CancellationToken cancellationToken)
    {
        var ingredient = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Feed ingredient with ID '{request.Id}' was not found.");

        var profile = new NutritionalProfile(
            request.DryMatterPct,
            request.CrudeProteinPct,
            request.MetabolizableEnergyMjPerKg,
            request.CrudeFiberPct,
            request.CalciumPct,
            request.PhosphorusPct);

        ingredient.UpdateDetails(
            request.Name,
            request.Category,
            profile,
            request.UnitCostBdt,
            request.Unit,
            request.Description);

        _repository.Update(ingredient);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
