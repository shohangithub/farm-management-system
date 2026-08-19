using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.ValueObjects;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedIngredients;

public sealed record CreateFeedIngredientCommand(
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
    string? Description = null,
    Guid? InventoryItemId = null) : IRequest<Guid>;

public sealed class CreateFeedIngredientCommandValidator : AbstractValidator<CreateFeedIngredientCommand>
{
    public CreateFeedIngredientCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.DryMatterPct).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.CrudeProteinPct).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
        RuleFor(x => x.MetabolizableEnergyMjPerKg).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCostBdt).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateFeedIngredientCommandHandler : IRequestHandler<CreateFeedIngredientCommand, Guid>
{
    private readonly IFeedIngredientRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFeedIngredientCommandHandler(
        IFeedIngredientRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateFeedIngredientCommand request, CancellationToken cancellationToken)
    {
        var profile = new NutritionalProfile(
            request.DryMatterPct,
            request.CrudeProteinPct,
            request.MetabolizableEnergyMjPerKg,
            request.CrudeFiberPct,
            request.CalciumPct,
            request.PhosphorusPct);

        var ingredient = new FeedIngredient(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.Name,
            request.Category,
            profile,
            request.UnitCostBdt,
            request.Unit,
            false,
            request.Description,
            request.InventoryItemId);

        await _repository.AddAsync(ingredient, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ingredient.Id;
    }
}
