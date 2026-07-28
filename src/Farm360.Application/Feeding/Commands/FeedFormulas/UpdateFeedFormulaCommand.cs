using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedFormulas;

public sealed record UpdateFeedFormulaCommand(
    Guid Id,
    string Title,
    TargetAnimalType TargetSpecies,
    FormulaStatus Status,
    string? TargetStage = null,
    string? Description = null,
    IReadOnlyList<FormulaIngredientRequest>? Ingredients = null) : IRequest;

public sealed class UpdateFeedFormulaCommandValidator : AbstractValidator<UpdateFeedFormulaCommand>
{
    public UpdateFeedFormulaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetSpecies).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class UpdateFeedFormulaCommandHandler : IRequestHandler<UpdateFeedFormulaCommand>
{
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _ingredientRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFeedFormulaCommandHandler(
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

    public async Task Handle(UpdateFeedFormulaCommand request, CancellationToken cancellationToken)
    {
        var formula = await _formulaRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Feed formula with ID '{request.Id}' was not found.");

        formula.UpdateDetails(request.Title, request.TargetSpecies, request.TargetStage, request.Description);

        if (request.Ingredients != null)
        {
            var allIngredients = await _ingredientRepository.GetAllAsync(_tenantService.TenantId, true, cancellationToken);
            var ingDict = allIngredients.ToDictionary(i => i.Id);

            var existingIds = formula.Ingredients.Select(i => i.IngredientId).ToList();
            foreach (var oldId in existingIds)
            {
                formula.RemoveIngredient(oldId);
            }

            foreach (var item in request.Ingredients)
            {
                if (ingDict.TryGetValue(item.IngredientId, out var ing))
                {
                    formula.AddIngredient(ing.Id, item.Percentage, ing.UnitCostBdt, ing.NutritionalProfile);
                }
            }
        }

        formula.SetStatus(request.Status);

        _formulaRepository.Update(formula);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
