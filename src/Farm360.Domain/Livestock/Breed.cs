using Farm360.Domain.Common;
using System;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Breed — Aggregate root representing a cattle breed.
/// Replaces the free-text BreedName and serves as the master data foundation
/// for the Smart Farm Intelligence engines (predictive growth, feed modeling, etc.).
/// </summary>
public sealed class Breed : AuditableEntity, IAggregateRoot
{
    private Breed() { } // EF Core required

    public Breed(
        Guid id,
        Guid tenantId,
        string name,
        string description,
        string category,
        string origin,
        string mainPurpose,
        decimal adgPoorManagement,
        decimal adgAverageFarm,
        decimal adgGoodCommercialFarm,
        decimal adgIntensiveFattening,
        decimal fcrMin,
        decimal fcrMax,
        decimal standardAdgMin,
        decimal standardAdgMax,
        decimal milkYieldMinLiters,
        decimal milkYieldMaxLiters,
        decimal fatPercentageMin,
        decimal fatPercentageMax,
        string bestFor) : base(id, tenantId)
    {
        Name = name;
        Description = description;
        Category = category;
        Origin = origin;
        MainPurpose = mainPurpose;
        AdgPoorManagement = adgPoorManagement;
        AdgAverageFarm = adgAverageFarm;
        AdgGoodCommercialFarm = adgGoodCommercialFarm;
        AdgIntensiveFattening = adgIntensiveFattening;
        FcrMin = fcrMin;
        FcrMax = fcrMax;
        StandardAdgMin = standardAdgMin;
        StandardAdgMax = standardAdgMax;
        MilkYieldMinLiters = milkYieldMinLiters;
        MilkYieldMaxLiters = milkYieldMaxLiters;
        FatPercentageMin = fatPercentageMin;
        FatPercentageMax = fatPercentageMax;
        BestFor = bestFor;
    }

    // ── Basic Info ─────────────────────────────────────────────────────────────
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty; // e.g., Indigenous, Exotic, Crossbred
    public string Origin { get; private set; } = string.Empty;
    public string MainPurpose { get; private set; } = string.Empty; // e.g., Dairy, Beef, Dual-purpose
    public string BestFor { get; private set; } = string.Empty;

    // ── Environmental Growth Targets (Average Daily Gain in kg) ────────────────
    public decimal AdgPoorManagement { get; private set; }
    public decimal AdgAverageFarm { get; private set; }
    public decimal AdgGoodCommercialFarm { get; private set; }
    public decimal AdgIntensiveFattening { get; private set; }

    // ── Standard Performance Ranges ───────────────────────────────────────────
    public decimal StandardAdgMin { get; private set; }
    public decimal StandardAdgMax { get; private set; }
    
    /// <summary>
    /// Feed Conversion Ratio (Dry Matter feed required for 1 kg of gain).
    /// </summary>
    public decimal FcrMin { get; private set; }
    public decimal FcrMax { get; private set; }

    // ── Dairy Metrics ─────────────────────────────────────────────────────────
    public decimal MilkYieldMinLiters { get; private set; }
    public decimal MilkYieldMaxLiters { get; private set; }
    public decimal FatPercentageMin { get; private set; }
    public decimal FatPercentageMax { get; private set; }

    // ── Domain Methods ────────────────────────────────────────────────────────
    
    public void UpdateDetails(
        string name, string description, string category, string origin, string mainPurpose, string bestFor)
    {
        Name = name;
        Description = description;
        Category = category;
        Origin = origin;
        MainPurpose = mainPurpose;
        BestFor = bestFor;
    }

    public void UpdateGrowthMetrics(
        decimal adgPoor, decimal adgAverage, decimal adgGood, decimal adgIntensive,
        decimal adgMin, decimal adgMax)
    {
        AdgPoorManagement = adgPoor;
        AdgAverageFarm = adgAverage;
        AdgGoodCommercialFarm = adgGood;
        AdgIntensiveFattening = adgIntensive;
        StandardAdgMin = adgMin;
        StandardAdgMax = adgMax;
    }

    public void UpdateEfficiencyMetrics(decimal fcrMin, decimal fcrMax)
    {
        FcrMin = fcrMin;
        FcrMax = fcrMax;
    }

    public void UpdateDairyMetrics(decimal yieldMin, decimal yieldMax, decimal fatMin, decimal fatMax)
    {
        MilkYieldMinLiters = yieldMin;
        MilkYieldMaxLiters = yieldMax;
        FatPercentageMin = fatMin;
        FatPercentageMax = fatMax;
    }
}
