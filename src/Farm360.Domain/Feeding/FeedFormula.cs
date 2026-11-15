using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Feeding.Exceptions;
using Farm360.Domain.Feeding.ValueObjects;

namespace Farm360.Domain.Feeding;

public sealed class FeedFormula : AuditableEntity, IAggregateRoot
{
    private readonly List<FormulaIngredient> _ingredients = new();

    public string Title { get; private set; } = null!;
    public TargetAnimalType TargetSpecies { get; private set; }
    public string? TargetStage { get; private set; }
    public FormulaStatus Status { get; private set; } = FormulaStatus.Draft;
    public string? Description { get; private set; }
    public decimal TotalCostPerKgBdt { get; private set; }
    public NutritionalProfile NutritionalProfile { get; private set; } = NutritionalProfile.Empty;

    public IReadOnlyCollection<FormulaIngredient> Ingredients => _ingredients.AsReadOnly();

    private FeedFormula() { } // EF Core

    public FeedFormula(
        Guid id,
        Guid tenantId,
        string title,
        TargetAnimalType targetSpecies,
        string? targetStage = null,
        string? description = null)
        : base(id, tenantId)
    {
        Title = title;
        TargetSpecies = targetSpecies;
        TargetStage = targetStage;
        Description = description;
        Status = FormulaStatus.Draft;

        RaiseDomainEvent(new FeedFormulaCreatedEvent(Id, TenantId, Title, TotalCostPerKgBdt));
    }

    public void UpdateDetails(
        string title,
        TargetAnimalType targetSpecies,
        string? targetStage,
        string? description)
    {
        Title = title;
        TargetSpecies = targetSpecies;
        TargetStage = targetStage;
        Description = description;
    }

    public void AddIngredient(Guid ingredientId, decimal percentage, decimal ingredientCostPerKg, NutritionalProfile ingredientProfile)
    {
        var existing = _ingredients.FirstOrDefault(i => i.IngredientId == ingredientId);
        if (existing is not null)
        {
            _ingredients.Remove(existing);
        }

        _ingredients.Add(new FormulaIngredient(Guid.NewGuid(), Id, ingredientId, percentage, ingredientCostPerKg, ingredientProfile));
        RecalculateTotals();
    }

    public void RemoveIngredient(Guid ingredientId)
    {
        var existing = _ingredients.FirstOrDefault(i => i.IngredientId == ingredientId);
        if (existing is not null)
        {
            _ingredients.Remove(existing);
            RecalculateTotals();
        }
    }

    public void SetStatus(FormulaStatus status)
    {
        if (status == FormulaStatus.Active && _ingredients.Count == 0)
        {
            throw new FeedingDomainException("Cannot activate a feed formula without ingredients.");
        }

        Status = status;
    }

    /// <summary>How far a recipe's percentages may stray from 100 and still be accepted.</summary>
    public const decimal PercentageTolerance = 0.01m;

    /// <summary>Sum of the ingredient percentages (100 for a well-formed recipe).</summary>
    public decimal PercentageTotal => _ingredients.Sum(i => i.Percentage);

    /// <summary>
    /// This ingredient's fraction of one kg of the recipe — its percentage over the sum of all the
    /// recipe's percentages, so a recipe saved at 99.9% or 101% still splits exactly one kg.
    /// The one place every consumer (stock deduction, costing, planning) gets its split from.
    /// </summary>
    public decimal ShareOf(FormulaIngredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        return Share(ingredient.Percentage, PercentageTotal);
    }

    /// <summary>A percentage's fraction of <paramref name="totalPercentage"/> (falls back to /100 when the total is 0).</summary>
    public static decimal Share(decimal percentage, decimal totalPercentage) =>
        totalPercentage > 0 ? percentage / totalPercentage : percentage / 100m;

    /// <summary>True when the percentages add up to 100 (± <see cref="PercentageTolerance"/>).</summary>
    public static bool PercentagesSumTo100(IEnumerable<decimal> percentages)
    {
        ArgumentNullException.ThrowIfNull(percentages);
        return Math.Abs(percentages.Sum() - 100m) <= PercentageTolerance;
    }

    public void RecalculateTotals()
    {
        if (_ingredients.Count == 0)
        {
            TotalCostPerKgBdt = 0;
            NutritionalProfile = NutritionalProfile.Empty;
            return;
        }

        decimal totalPercentage = PercentageTotal;

        decimal costSum = 0;
        decimal dmSum = 0;
        decimal cpSum = 0;
        decimal meSum = 0;
        decimal cfSum = 0;
        decimal caSum = 0;
        decimal pSum = 0;

        foreach (var item in _ingredients)
        {
            decimal ratio = Share(item.Percentage, totalPercentage);
            costSum += item.IngredientCostPerKg * ratio;
            dmSum += item.IngredientNutritionalProfile.DryMatterPercentage * ratio;
            cpSum += item.IngredientNutritionalProfile.CrudeProteinPercentage * ratio;
            meSum += item.IngredientNutritionalProfile.MetabolizableEnergyMjPerKg * ratio;
            cfSum += item.IngredientNutritionalProfile.CrudeFiberPercentage * ratio;
            caSum += item.IngredientNutritionalProfile.CalciumPercentage * ratio;
            pSum += item.IngredientNutritionalProfile.PhosphorusPercentage * ratio;
        }

        TotalCostPerKgBdt = Math.Round(costSum, 2);
        NutritionalProfile = new NutritionalProfile(dmSum, cpSum, meSum, cfSum, caSum, pSum);
    }
}

public sealed class FormulaIngredient : BaseEntity
{
    public Guid FormulaId { get; private set; }
    public Guid IngredientId { get; private set; }
    public decimal Percentage { get; private set; }
    public decimal IngredientCostPerKg { get; private set; }
    public NutritionalProfile IngredientNutritionalProfile { get; private set; } = null!;

    private FormulaIngredient() { } // EF Core

    public FormulaIngredient(
        Guid id,
        Guid formulaId,
        Guid ingredientId,
        decimal percentage,
        decimal ingredientCostPerKg,
        NutritionalProfile ingredientNutritionalProfile)
        : base(id)
    {
        FormulaId = formulaId;
        IngredientId = ingredientId;
        Percentage = Math.Max(0, percentage);
        IngredientCostPerKg = Math.Max(0, ingredientCostPerKg);
        IngredientNutritionalProfile = ingredientNutritionalProfile;
    }
}
