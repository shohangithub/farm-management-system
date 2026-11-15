using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedFormulas;

public record FormulaIngredientRequest(Guid IngredientId, decimal Percentage);

public sealed record CreateFeedFormulaCommand(
    string Title,
    TargetAnimalType TargetSpecies,
    string? TargetStage = null,
    string? Description = null,
    IReadOnlyList<FormulaIngredientRequest>? Ingredients = null) : IRequest<Guid>;

public sealed class CreateFeedFormulaCommandValidator : AbstractValidator<CreateFeedFormulaCommand>
{
    public CreateFeedFormulaCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetSpecies).IsInEnum();
        RuleFor(x => x.Ingredients)
            .Must(i => i is null || i.Count == 0 || Farm360.Domain.Feeding.FeedFormula.PercentagesSumTo100(i.Select(x => x.Percentage)))
            .WithMessage("Ingredient percentages must add up to 100%.");
        RuleForEach(x => x.Ingredients).Must(i => i.Percentage > 0 && i.Percentage <= 100)
            .WithMessage("Each ingredient percentage must be between 0 and 100.");
    }
}

public sealed class CreateFeedFormulaCommandHandler : IRequestHandler<CreateFeedFormulaCommand, Guid>
{
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _ingredientRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFeedFormulaCommandHandler(
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository ingredientRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _formulaRepository = formulaRepository;
        _ingredientRepository = ingredientRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateFeedFormulaCommand request, CancellationToken cancellationToken)
    {
        var formula = new FeedFormula(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.Title,
            request.TargetSpecies,
            request.TargetStage,
            request.Description);

        if (request.Ingredients != null && request.Ingredients.Count > 0)
        {
            var allIngredients = await _ingredientRepository.GetAllAsync(_tenantService.TenantId, true, cancellationToken);
            var ingDict = allIngredients.ToDictionary(i => i.Id);

            foreach (var item in request.Ingredients)
            {
                if (ingDict.TryGetValue(item.IngredientId, out var ing))
                {
                    formula.AddIngredient(ing.Id, item.Percentage, ing.UnitCostBdt, ing.NutritionalProfile);
                }
            }
        }

        await _formulaRepository.AddAsync(formula, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return formula.Id;
    }
}
