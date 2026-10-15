using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance.Accounting;

public enum FixedAssetCategory
{
    Land = 0,
    BuildingAndShed = 1,
    MachineryAndEquipment = 2,
    Vehicle = 3,
    FurnitureAndOffice = 4,
    Other = 5,
}

public enum FixedAssetAcquisition
{
    /// <summary>Paid in cash: Dr Property, plant &amp; equipment / Cr Cash.</summary>
    Cash = 0,

    /// <summary>Bought on credit: Dr Property, plant &amp; equipment / Cr Accounts payable (a supplier bill).</summary>
    Credit = 1,

    /// <summary>Brought in by the owner: Dr Property, plant &amp; equipment / Cr Owner's capital.</summary>
    OwnerContributed = 2,

    /// <summary>
    /// Owned before the books start: carried at its audited written-down value as at the start date,
    /// as part of the opening position (no posting).
    /// </summary>
    Opening = 3,
}

/// <summary>A shed, machine, vehicle or plot of land the farm owns and uses for more than a year.</summary>
public sealed class FixedAsset : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public FixedAssetCategory Category { get; private set; }
    public FixedAssetAcquisition Acquisition { get; private set; }

    /// <summary>Purchase date — or, for an opening asset, the date its written-down value applies to.</summary>
    public DateOnly AcquisitionDate { get; private set; }

    /// <summary>Purchase cost — or, for an opening asset, its written-down value on <see cref="AcquisitionDate"/>.</summary>
    public decimal Cost { get; private set; }

    public DepreciationMethod Method { get; private set; }
    public decimal RatePercent { get; private set; }
    public decimal UsefulLifeYears { get; private set; }
    public decimal ResidualValue { get; private set; }

    public DateOnly? DisposalDate { get; private set; }
    public decimal DisposalProceeds { get; private set; }

    public string? Location { get; private set; }
    public string? Notes { get; private set; }

    public bool IsDisposed => DisposalDate.HasValue;

    /// <summary>What depreciation is charged from: cost from the purchase date, or the opening WDV from the day after.</summary>
    public DepreciationBasis Basis => new(
        Acquisition == FixedAssetAcquisition.Opening ? AcquisitionDate.AddDays(1) : AcquisitionDate,
        Cost,
        Method,
        Category == FixedAssetCategory.Land ? 0m : RatePercent,
        Category == FixedAssetCategory.Land ? 0m : UsefulLifeYears,
        ResidualValue);

    private FixedAsset() { } // EF Core

    private FixedAsset(Guid tenantId, Guid farmId, string code, FixedAssetAcquisition acquisition) : base(Guid.NewGuid(), tenantId)
    {
        FarmId = farmId;
        Code = code;
        Acquisition = acquisition;
    }

    public static FixedAsset Create(
        Guid tenantId,
        Guid farmId,
        string code,
        string name,
        FixedAssetCategory category,
        FixedAssetAcquisition acquisition,
        DateOnly acquisitionDate,
        decimal cost,
        DepreciationMethod method,
        decimal ratePercent,
        decimal usefulLifeYears,
        decimal residualValue,
        string? location = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var asset = new FixedAsset(tenantId, farmId, code.Trim(), acquisition);
        asset.Update(name, category, acquisitionDate, cost, method, ratePercent, usefulLifeYears, residualValue, location, notes);
        return asset;
    }

    /// <summary>How it was acquired cannot change: that decides its ledger posting. Delete and re-register instead.</summary>
    public void Update(
        string name,
        FixedAssetCategory category,
        DateOnly acquisitionDate,
        decimal cost,
        DepreciationMethod method,
        decimal ratePercent,
        decimal usefulLifeYears,
        decimal residualValue,
        string? location = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (cost <= 0)
            throw new ArgumentException("Cost must be greater than zero.", nameof(cost));
        if (ratePercent is < 0 or > 100)
            throw new ArgumentException("The depreciation rate must be between 0 and 100%.", nameof(ratePercent));
        if (usefulLifeYears < 0)
            throw new ArgumentException("Useful life cannot be negative.", nameof(usefulLifeYears));
        if (residualValue < 0 || residualValue > cost)
            throw new ArgumentException("Residual value must be between zero and the cost.", nameof(residualValue));
        if (category != FixedAssetCategory.Land)
        {
            if (method == DepreciationMethod.ReducingBalance && ratePercent <= 0)
                throw new ArgumentException("Enter the yearly depreciation rate.", nameof(ratePercent));
            if (method == DepreciationMethod.StraightLine && usefulLifeYears <= 0)
                throw new ArgumentException("Enter the useful life in years.", nameof(usefulLifeYears));
        }
        if (DisposalDate is { } disposed && disposed < acquisitionDate)
            throw new ArgumentException("The asset was disposed of before this acquisition date.", nameof(acquisitionDate));

        Name = name.Trim();
        Category = category;
        AcquisitionDate = acquisitionDate;
        Cost = Math.Round(cost, 2);
        Method = method;
        RatePercent = ratePercent;
        UsefulLifeYears = usefulLifeYears;
        ResidualValue = Math.Round(residualValue, 2);
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void RecordDisposal(DateOnly disposalDate, decimal proceeds)
    {
        if (disposalDate < AcquisitionDate)
            throw new ArgumentException("An asset cannot be disposed of before it was acquired.", nameof(disposalDate));
        if (proceeds < 0)
            throw new ArgumentException("Sale proceeds cannot be negative.", nameof(proceeds));

        DisposalDate = disposalDate;
        DisposalProceeds = Math.Round(proceeds, 2);
    }

    public void CancelDisposal()
    {
        DisposalDate = null;
        DisposalProceeds = 0m;
    }
}
