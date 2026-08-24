using Farm360.Domain.Common;
using System;

namespace Farm360.Domain.Intelligence.Projections;

public sealed class ProjectionScenario : AuditableEntity, IAggregateRoot
{
    private ProjectionScenario() { } // EF Core

    public ProjectionScenario(
        Guid id,
        Guid tenantId,
        Guid? animalId,
        string name,
        string description,
        FatteningProjectionInputs inputs) : base(id, tenantId)
    {
        AnimalId = animalId;
        Name = name;
        Description = description;
        StartingLiveWeightKg = inputs.StartingLiveWeightKg;
        PurchasePriceBdt = inputs.PurchasePriceBdt;
        CurrentMeatPriceBdtPerKg = inputs.CurrentMeatPriceBdtPerKg;
        InitialMeatYieldRatio = inputs.InitialMeatYieldRatio;
        DailyLiveWeightGainKg = inputs.DailyLiveWeightGainKg;
        MeatYieldOnDailyGainRatio = inputs.MeatYieldOnDailyGainRatio;
        DailyFeedQuantityKgAtStart = inputs.DailyFeedQuantityKgAtStart;
        FeedPriceBdtPerKg = inputs.FeedPriceBdtPerKg;
        DailyGrassCostBdt = inputs.DailyGrassCostBdt;
        DailyOtherCostBdt = inputs.DailyOtherCostBdt;
        MonthlyLaborCostBdt = inputs.MonthlyLaborCostBdt;
        FatteningPeriodDays = inputs.FatteningPeriodDays;
    }

    public Guid? AnimalId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    // Snapshot of Inputs
    public decimal StartingLiveWeightKg { get; private set; }
    public decimal PurchasePriceBdt { get; private set; }
    public decimal CurrentMeatPriceBdtPerKg { get; private set; }
    public decimal InitialMeatYieldRatio { get; private set; }
    public decimal DailyLiveWeightGainKg { get; private set; }
    public decimal MeatYieldOnDailyGainRatio { get; private set; }
    public decimal DailyFeedQuantityKgAtStart { get; private set; }
    public decimal FeedPriceBdtPerKg { get; private set; }
    public decimal DailyGrassCostBdt { get; private set; }
    public decimal DailyOtherCostBdt { get; private set; }
    public decimal MonthlyLaborCostBdt { get; private set; }
    public int FatteningPeriodDays { get; private set; }

    public FatteningProjectionInputs Inputs => new FatteningProjectionInputs(
        StartingLiveWeightKg,
        PurchasePriceBdt,
        CurrentMeatPriceBdtPerKg,
        InitialMeatYieldRatio,
        DailyLiveWeightGainKg,
        MeatYieldOnDailyGainRatio,
        DailyFeedQuantityKgAtStart,
        FeedPriceBdtPerKg,
        DailyGrassCostBdt,
        DailyOtherCostBdt,
        MonthlyLaborCostBdt,
        FatteningPeriodDays);
}
