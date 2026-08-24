using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance;

/// <summary>
/// AnimalCostLedger — Aggregate root tracking the running cost of ownership per animal.
/// PRD FR-FM-06: "The system shall maintain a running cost ledger per individual animal
/// from acquisition to disposal."
/// PRD FR-FM-09: Break-even sale price based on accumulated costs.
/// </summary>
public sealed class AnimalCostLedger : AuditableEntity, IAggregateRoot
{
    public Guid AnimalId { get; private set; }
    public Guid FarmId { get; private set; }

    // ── Cost Buckets ────────────────────────────────────────────────────────
    public decimal AcquisitionCostBdt { get; private set; }
    public decimal TotalFeedCostBdt { get; private set; }
    public decimal TotalVetCostBdt { get; private set; }
    public decimal TotalLaborCostBdt { get; private set; }
    public decimal TotalOverheadBdt { get; private set; }

    /// <summary>
    /// Computed total cost of ownership: Acquisition + Feed + Vet + Labor + Overhead.
    /// </summary>
    public decimal TotalCostBdt => AcquisitionCostBdt + TotalFeedCostBdt + TotalVetCostBdt + TotalLaborCostBdt + TotalOverheadBdt;

    // ── Sale Tracking ───────────────────────────────────────────────────────
    public decimal? SaleRevenueBdt { get; private set; }
    public decimal? ProfitLossBdt => SaleRevenueBdt.HasValue ? SaleRevenueBdt.Value - TotalCostBdt : null;

    private AnimalCostLedger() { } // For EF Core

    public static AnimalCostLedger Create(
        Guid tenantId,
        Guid animalId,
        Guid farmId,
        decimal acquisitionCostBdt = 0)
    {
        if (acquisitionCostBdt < 0)
            throw new ArgumentException("Acquisition cost cannot be negative.", nameof(acquisitionCostBdt));

        var ledger = new AnimalCostLedger
        {
            Id = Guid.NewGuid(),
            AnimalId = animalId,
            FarmId = farmId,
            AcquisitionCostBdt = acquisitionCostBdt,
            TotalFeedCostBdt = 0,
            TotalVetCostBdt = 0,
            TotalLaborCostBdt = 0,
            TotalOverheadBdt = 0
        };

        ledger.SetTenantId(tenantId);
        return ledger;
    }

    /// <summary>
    /// Accumulates a cost into the appropriate bucket based on the transaction category.
    /// </summary>
    public void RecordCost(Enums.TransactionCategory category, decimal amountBdt)
    {
        if (amountBdt < 0)
            throw new ArgumentException("Cost amount cannot be negative.", nameof(amountBdt));

        switch (category)
        {
            case Enums.TransactionCategory.FeedCost:
                TotalFeedCostBdt += amountBdt;
                break;
            case Enums.TransactionCategory.VeterinaryCost:
            case Enums.TransactionCategory.MedicineCost:
                TotalVetCostBdt += amountBdt;
                break;
            case Enums.TransactionCategory.LaborCost:
                TotalLaborCostBdt += amountBdt;
                break;
            case Enums.TransactionCategory.AnimalPurchase:
                AcquisitionCostBdt += amountBdt;
                break;
            default:
                TotalOverheadBdt += amountBdt;
                break;
        }
    }

    /// <summary>
    /// FR-FM-09: Returns the break-even sale price per kg based on accumulated costs.
    /// </summary>
    public decimal GetBreakEvenPricePerKg(decimal currentWeightKg)
    {
        if (currentWeightKg <= 0)
            return 0;

        return Math.Round(TotalCostBdt / currentWeightKg, 2);
    }

    /// <summary>
    /// Records the sale revenue when the animal is sold.
    /// </summary>
    public void RecordSaleRevenue(decimal salePriceBdt)
    {
        if (salePriceBdt < 0)
            throw new ArgumentException("Sale revenue cannot be negative.", nameof(salePriceBdt));

        SaleRevenueBdt = salePriceBdt;
    }
}
