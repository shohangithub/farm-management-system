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

    public void RecalculateTotals()
    {
        if (_ingredients.Count == 0)
        {
            TotalCostPerKgBdt = 0;
            NutritionalProfile = NutritionalProfile.Empty;
            return;
        }

        decimal totalPercentage = _ingredients.Sum(i => i.Percentage);
        if (totalPercentage <= 0) totalPercentage = 100;

        decimal costSum = 0;
        decimal dmSum = 0;
        decimal cpSum = 0;
        decimal meSum = 0;
        decimal cfSum = 0;
        decimal caSum = 0;
        decimal pSum = 0;

        foreach (var item in _ingredients)
        {
            decimal ratio = item.Percentage / totalPercentage;
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
