using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.BusinessRules;

/// <summary>
/// The values a customer may override in <see cref="FarmBusinessRules"/>. A <c>null</c> field means
/// "not overridden here": the value is inherited from the next level (farm → tenant → platform config).
/// </summary>
/// <remarks>
/// Platform-only fields (the QuestPDF licence tier and key) are deliberately absent: they describe the
/// deployment, not a customer's business judgement, and must never be editable by a tenant.
/// </remarks>
public sealed record BusinessRulesOverrides(
    FeedAllocationMethod? FeedAllocation = null,
    double? MetabolicWeightExponent = null,
    GroupPlanQuantityBasis? GroupPlanQuantityBasis = null,
    decimal? FallbackAnimalWeightKg = null,
    OverheadAllocationMethod? OverheadAllocation = null,
    OverheadCostPeriod? OverheadCostPeriod = null,
    AnimalValuationPolicy? Valuation = null,
    decimal? DefaultDressingPercentage = null,
    decimal? DefaultMeatPricePerKgBdt = null,
    decimal? DefaultLiveWeightPricePerKgBdt = null,
    ReportLanguagePreference? DefaultReportLanguage = null,
    bool? BengaliNumeralsByDefault = null)
{
    /// <summary>True when no field is overridden.</summary>
    public bool IsEmpty =>
        FeedAllocation is null && MetabolicWeightExponent is null && GroupPlanQuantityBasis is null &&
        FallbackAnimalWeightKg is null && OverheadAllocation is null && OverheadCostPeriod is null &&
        Valuation is null && DefaultDressingPercentage is null && DefaultMeatPricePerKgBdt is null &&
        DefaultLiveWeightPricePerKgBdt is null && DefaultReportLanguage is null && BengaliNumeralsByDefault is null;
}

/// <summary>
/// A customer's own business assumptions (valuation, feed and overhead allocation, report language),
/// layered over the platform defaults in <c>Farm360:BusinessRules</c>.
/// </summary>
/// <remarks>
/// <para>
/// One row per tenant (<see cref="FarmId"/> is <c>null</c>), plus an optional row per farm that
/// overrides individual fields for that farm only. Resolution is field by field: farm → tenant → config.
/// </para>
/// <para>
/// Changing a value affects future calculations only. Feed and overhead allocations persist the method
/// that produced them, so historical reports keep the numbers they were signed off with.
/// </para>
/// </remarks>
public sealed class TenantBusinessSettings : AuditableEntity, IAggregateRoot
{
    /// <summary>The farm this row overrides, or <c>null</c> for the tenant-wide row.</summary>
    public Guid? FarmId { get; private set; }

    public FeedAllocationMethod? FeedAllocation { get; private set; }
    public double? MetabolicWeightExponent { get; private set; }
    public GroupPlanQuantityBasis? GroupPlanQuantityBasis { get; private set; }
    public decimal? FallbackAnimalWeightKg { get; private set; }
    public OverheadAllocationMethod? OverheadAllocation { get; private set; }
    public OverheadCostPeriod? OverheadCostPeriod { get; private set; }
    public AnimalValuationPolicy? Valuation { get; private set; }
    public decimal? DefaultDressingPercentage { get; private set; }
    public decimal? DefaultMeatPricePerKgBdt { get; private set; }
    public decimal? DefaultLiveWeightPricePerKgBdt { get; private set; }
    public ReportLanguagePreference? DefaultReportLanguage { get; private set; }
    public bool? BengaliNumeralsByDefault { get; private set; }

    private TenantBusinessSettings() { } // EF Core

    private TenantBusinessSettings(Guid tenantId, Guid? farmId) : base(Guid.NewGuid(), tenantId)
    {
        if (farmId == Guid.Empty)
            throw new ArgumentException("FarmId must be null (tenant-wide) or a real farm.", nameof(farmId));

        FarmId = farmId;
    }

    public static TenantBusinessSettings Create(Guid tenantId, Guid? farmId, BusinessRulesOverrides values)
    {
        var settings = new TenantBusinessSettings(tenantId, farmId);
        settings.Update(values);
        return settings;
    }

    /// <summary>Replaces every overridable field. A <c>null</c> value reverts that field to the inherited default.</summary>
    public void Update(BusinessRulesOverrides values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Guard(values.MetabolicWeightExponent is null or (> 0d and <= 1.5d), nameof(values.MetabolicWeightExponent));
        Guard(values.FallbackAnimalWeightKg is null or > 0m, nameof(values.FallbackAnimalWeightKg));
        Guard(values.DefaultDressingPercentage is null or (>= 0.3m and <= 0.7m), nameof(values.DefaultDressingPercentage));
        Guard(values.DefaultMeatPricePerKgBdt is null or > 0m, nameof(values.DefaultMeatPricePerKgBdt));
        Guard(values.DefaultLiveWeightPricePerKgBdt is null or > 0m, nameof(values.DefaultLiveWeightPricePerKgBdt));

        FeedAllocation = values.FeedAllocation;
        MetabolicWeightExponent = values.MetabolicWeightExponent;
        GroupPlanQuantityBasis = values.GroupPlanQuantityBasis;
        FallbackAnimalWeightKg = values.FallbackAnimalWeightKg;
        OverheadAllocation = values.OverheadAllocation;
        OverheadCostPeriod = values.OverheadCostPeriod;
        Valuation = values.Valuation;
        DefaultDressingPercentage = values.DefaultDressingPercentage;
        DefaultMeatPricePerKgBdt = values.DefaultMeatPricePerKgBdt;
        DefaultLiveWeightPricePerKgBdt = values.DefaultLiveWeightPricePerKgBdt;
        DefaultReportLanguage = values.DefaultReportLanguage;
        BengaliNumeralsByDefault = values.BengaliNumeralsByDefault;
    }

    /// <summary>The stored values, <c>null</c> where this row inherits.</summary>
    public BusinessRulesOverrides ToOverrides() => new(
        FeedAllocation, MetabolicWeightExponent, GroupPlanQuantityBasis, FallbackAnimalWeightKg,
        OverheadAllocation, OverheadCostPeriod, Valuation, DefaultDressingPercentage,
        DefaultMeatPricePerKgBdt, DefaultLiveWeightPricePerKgBdt, DefaultReportLanguage, BengaliNumeralsByDefault);

    private static void Guard(bool ok, string field)
    {
        if (!ok) throw new ArgumentOutOfRangeException(field, $"{field} is outside its allowed range.");
    }
}

/// <summary>Layers <see cref="BusinessRulesOverrides"/> over a baseline <see cref="FarmBusinessRules"/>.</summary>
public static class FarmBusinessRulesResolution
{
    /// <summary>
    /// Returns a new rules object: <paramref name="baseline"/> with every non-null override applied.
    /// The baseline is never mutated, so a cached or config-bound instance stays intact.
    /// </summary>
    public static FarmBusinessRules Apply(FarmBusinessRules baseline, BusinessRulesOverrides? overrides)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var r = baseline.Clone();
        if (overrides is null) return r;

        if (overrides.FeedAllocation is { } fa) r.FeedAllocation = fa;
        if (overrides.MetabolicWeightExponent is { } mwe) r.MetabolicWeightExponent = mwe;
        if (overrides.GroupPlanQuantityBasis is { } gpb) r.GroupPlanQuantityBasis = gpb;
        if (overrides.FallbackAnimalWeightKg is { } fw) r.FallbackAnimalWeightKg = fw;
        if (overrides.OverheadAllocation is { } oa) r.OverheadAllocation = oa;
        if (overrides.OverheadCostPeriod is { } ocp) r.OverheadCostPeriod = ocp;
        if (overrides.Valuation is { } v) r.Valuation = v;
        if (overrides.DefaultDressingPercentage is { } dp) r.DefaultDressingPercentage = dp;
        if (overrides.DefaultMeatPricePerKgBdt is { } mp) r.DefaultMeatPricePerKgBdt = mp;
        if (overrides.DefaultLiveWeightPricePerKgBdt is { } lp) r.DefaultLiveWeightPricePerKgBdt = lp;
        if (overrides.DefaultReportLanguage is { } lang) r.DefaultReportLanguage = lang;
        if (overrides.BengaliNumeralsByDefault is { } bn) r.BengaliNumeralsByDefault = bn;
        return r;
    }
}
