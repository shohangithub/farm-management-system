using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Events;

namespace Farm360.Domain.Feeding;

public sealed class DailyFeedingEntry : AuditableEntity, IAggregateRoot
{
    public Guid FeedingPlanId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid? ShedId { get; private set; }
    public Guid? PenId { get; private set; }
    public Guid? BatchId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public Guid FormulaId { get; private set; }
    public Guid? RuleLineId { get; private set; }
    public decimal ExpectedKg { get; private set; }
    public decimal? ActualKg { get; private set; }
    public decimal? UnitCostAtConsumptionBdt { get; private set; }
    public decimal? TotalCostBdt { get; private set; }
    public DailyFeedingEntryStatus Status { get; private set; }
    public string? AdjustmentReason { get; private set; }
    public Guid? InventoryTransactionId { get; private set; }

    private DailyFeedingEntry() { }

    public DailyFeedingEntry(
        Guid id,
        Guid tenantId,
        Guid feedingPlanId,
        Guid farmId,
        DateOnly entryDate,
        Guid formulaId,
        decimal expectedKg,
        Guid? ruleLineId = null,
        Guid? shedId = null,
        Guid? penId = null,
        Guid? batchId = null)
        : base(id, tenantId)
    {
        FeedingPlanId = feedingPlanId;
        FarmId = farmId;
        EntryDate = entryDate;
        FormulaId = formulaId;
        RuleLineId = ruleLineId;
        ExpectedKg = expectedKg;
        ShedId = shedId;
        PenId = penId;
        BatchId = batchId;
        Status = DailyFeedingEntryStatus.Pending;
    }

    public void Confirm(decimal actualKg, Guid? inventoryTransactionId = null, string? adjustmentReason = null)
    {
        if (Status == DailyFeedingEntryStatus.Confirmed || Status == DailyFeedingEntryStatus.Adjusted)
            return;

        ActualKg = actualKg;
        InventoryTransactionId = inventoryTransactionId;
        AdjustmentReason = adjustmentReason;
        
        if (actualKg == ExpectedKg)
        {
            Status = DailyFeedingEntryStatus.Confirmed;
        }
        else
        {
            Status = DailyFeedingEntryStatus.Adjusted;
        }

        if (UnitCostAtConsumptionBdt.HasValue)
        {
            TotalCostBdt = Math.Round(actualKg * UnitCostAtConsumptionBdt.Value, 2);
        }

        RaiseDomainEvent(new DailyEntryConfirmedEvent(Guid.NewGuid(), DateTime.UtcNow, Id, TenantId, FarmId, actualKg, inventoryTransactionId));
    }

    public void SetConsumptionCost(decimal unitCostBdt)
    {
        UnitCostAtConsumptionBdt = Math.Max(0, unitCostBdt);
        var kg = ActualKg ?? ExpectedKg;
        TotalCostBdt = Math.Round(kg * UnitCostAtConsumptionBdt.Value, 2);
    }

    public void SetInventoryTransactionId(Guid transactionId)
    {
        InventoryTransactionId = transactionId;
    }

    public void Skip(string reason)
    {
        if (Status == DailyFeedingEntryStatus.Confirmed || Status == DailyFeedingEntryStatus.Adjusted)
            throw new InvalidOperationException("Cannot skip an already confirmed entry.");

        Status = DailyFeedingEntryStatus.Skipped;
        AdjustmentReason = reason;
        ActualKg = 0;
    }
}
