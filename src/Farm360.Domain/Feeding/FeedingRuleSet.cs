using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;

namespace Farm360.Domain.Feeding;

public sealed class FeedingRuleSet : AuditableEntity, IAggregateRoot
{
    private readonly List<FeedingRuleLine> _lines = new();

    public string Name { get; private set; } = null!;
    public FeedingPlanType PlanType { get; private set; } = FeedingPlanType.FixedQuantity;
    public TargetAnimalType Species { get; private set; }
    public FeedingPurpose Purpose { get; private set; }
    public Guid? BreedId { get; private set; }
    public int? AgeFromDays { get; private set; }
    public int? AgeToDays { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? BaseNotes { get; private set; }

    public IReadOnlyCollection<FeedingRuleLine> Lines => _lines;

    private FeedingRuleSet() { }

    public FeedingRuleSet(
        Guid id,
        Guid tenantId,
        string name,
        TargetAnimalType species,
        FeedingPurpose purpose,
        FeedingPlanType planType = FeedingPlanType.FixedQuantity,
        string? baseNotes = null,
        Guid? breedId = null,
        int? ageFromDays = null,
        int? ageToDays = null,
        bool isActive = true)
        : base(id, tenantId)
    {
        Name = name;
        Species = species;
        Purpose = purpose;
        PlanType = planType;
        BaseNotes = baseNotes;
        BreedId = breedId;
        AgeFromDays = ageFromDays;
        AgeToDays = ageToDays;
        IsActive = isActive;
    }

    public void UpdateDetails(
        string name,
        TargetAnimalType species,
        FeedingPurpose purpose,
        FeedingPlanType planType = FeedingPlanType.FixedQuantity,
        string? baseNotes = null,
        Guid? breedId = null,
        int? ageFromDays = null,
        int? ageToDays = null)
    {
        Name = name;
        Species = species;
        Purpose = purpose;
        PlanType = planType;
        BaseNotes = baseNotes;
        BreedId = breedId;
        AgeFromDays = ageFromDays;
        AgeToDays = ageToDays;
    }

    public void AddRuleLine(
        decimal? minWeightKg,
        decimal? maxWeightKg,
        int? minAgeDays,
        int? maxAgeDays,
        FeedCategory feedType,
        decimal quantityValue,
        Guid? formulaId = null,
        int sessionsPerDay = 1)
    {
        var line = new FeedingRuleLine(
            Guid.NewGuid(),
            Id,
            minWeightKg,
            maxWeightKg,
            minAgeDays,
            maxAgeDays,
            feedType,
            quantityValue,
            formulaId ?? Guid.Empty,
            sessionsPerDay);

        _lines.Add(line);
    }

    public void AddRuleLine(decimal weightFromKg, decimal weightToKg, Guid formulaId, decimal concentrateKgPerDay, decimal roughageKgPerDay, int sessionsPerDay, decimal? proteinTargetPercent = null)
    {
        var line = new FeedingRuleLine(
            Guid.NewGuid(),
            Id,
            weightFromKg,
            weightToKg,
            null,
            null,
            FeedCategory.Concentrate,
            concentrateKgPerDay,
            formulaId,
            sessionsPerDay,
            proteinTargetPercent);

        _lines.Add(line);
    }

    public void RemoveRuleLine(Guid lineId)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line != null)
        {
            _lines.Remove(line);
        }
    }

    public void ClearLines()
    {
        _lines.Clear();
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
    }
}

public sealed class FeedingRuleLine : BaseEntity
{
    public Guid FeedingRuleSetId { get; private set; }
    public decimal? MinWeightKg { get; private set; }
    public decimal? MaxWeightKg { get; private set; }
    public int? MinAgeDays { get; private set; }
    public int? MaxAgeDays { get; private set; }
    public FeedCategory FeedType { get; private set; }
    public decimal QuantityValue { get; private set; }
    public decimal WeightFromKg { get; private set; }
    public decimal WeightToKg { get; private set; }
    public Guid FormulaId { get; private set; }
    public decimal ConcentrateKgPerDay { get; private set; }
    public decimal RoughageKgPerDay { get; private set; }
    public int SessionsPerDay { get; private set; }
    public decimal? ProteinTargetPercent { get; private set; }

    private FeedingRuleLine() { }

    internal FeedingRuleLine(
        Guid id,
        Guid feedingRuleSetId,
        decimal? minWeightKg,
        decimal? maxWeightKg,
        int? minAgeDays,
        int? maxAgeDays,
        FeedCategory feedType,
        decimal quantityValue,
        Guid formulaId,
        int sessionsPerDay = 1,
        decimal? proteinTargetPercent = null)
        : base(id)
    {
        FeedingRuleSetId = feedingRuleSetId;
        MinWeightKg = minWeightKg;
        MaxWeightKg = maxWeightKg;
        MinAgeDays = minAgeDays;
        MaxAgeDays = maxAgeDays;
        FeedType = feedType;
        QuantityValue = quantityValue;
        WeightFromKg = minWeightKg ?? 0;
        WeightToKg = maxWeightKg ?? 0;
        ConcentrateKgPerDay = quantityValue;
        FormulaId = formulaId;
        SessionsPerDay = sessionsPerDay;
        ProteinTargetPercent = proteinTargetPercent;
    }
}
