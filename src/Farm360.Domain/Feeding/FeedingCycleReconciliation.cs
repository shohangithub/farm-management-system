using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Events;

namespace Farm360.Domain.Feeding;

public sealed class FeedingCycleReconciliation : AuditableEntity, IAggregateRoot
{
    private readonly List<FeedingCycleReconciliationLine> _lines = new();

    public Guid FarmId { get; private set; }
    public Guid? PlanId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal TotalExpectedKg { get; private set; }
    public decimal TotalActualKg { get; private set; }
    public decimal VarianceKg => TotalActualKg - TotalExpectedKg;
    public ReconciliationStatus Status { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }

    public IReadOnlyCollection<FeedingCycleReconciliationLine> Lines => _lines.AsReadOnly();

    private FeedingCycleReconciliation() { }

    public FeedingCycleReconciliation(
        Guid id,
        Guid tenantId,
        Guid farmId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal totalExpectedKg,
        decimal totalActualKg,
        Guid? planId = null)
        : base(id, tenantId)
    {
        FarmId = farmId;
        PlanId = planId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        TotalExpectedKg = totalExpectedKg;
        TotalActualKg = totalActualKg;
        Status = ReconciliationStatus.Pending;
    }

    public void AddLine(Guid inventoryItemId, decimal expectedQty, decimal actualQty)
    {
        var line = new FeedingCycleReconciliationLine(Guid.NewGuid(), Id, inventoryItemId, expectedQty, actualQty);
        _lines.Add(line);
    }

    public void Approve(Guid approvedByUserId)
    {
        if (Status != ReconciliationStatus.Pending && Status != ReconciliationStatus.Reviewed)
            throw new InvalidOperationException("Only Pending or Reviewed reconciliations can be approved.");

        Status = ReconciliationStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ReconciliationApprovedEvent(Guid.NewGuid(), DateTime.UtcNow, Id, TenantId, FarmId));
    }

    public void Reject(Guid rejectedByUserId)
    {
        if (Status != ReconciliationStatus.Pending && Status != ReconciliationStatus.Reviewed)
            throw new InvalidOperationException("Only Pending or Reviewed reconciliations can be rejected.");

        Status = ReconciliationStatus.Rejected;
        // Optionally store the rejector
    }
}

public sealed class FeedingCycleReconciliationLine : BaseEntity
{
    public Guid FeedingCycleReconciliationId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public decimal ExpectedQty { get; private set; }
    public decimal ActualQty { get; private set; }
    public decimal VarianceQty => ActualQty - ExpectedQty;
    public Guid? AdjustmentTransactionId { get; private set; }

    private FeedingCycleReconciliationLine() { }

    internal FeedingCycleReconciliationLine(
        Guid id,
        Guid feedingCycleReconciliationId,
        Guid inventoryItemId,
        decimal expectedQty,
        decimal actualQty)
        : base(id)
    {
        FeedingCycleReconciliationId = feedingCycleReconciliationId;
        InventoryItemId = inventoryItemId;
        ExpectedQty = expectedQty;
        ActualQty = actualQty;
    }

    public void SetAdjustmentTransactionId(Guid adjustmentTransactionId)
    {
        AdjustmentTransactionId = adjustmentTransactionId;
    }
}
