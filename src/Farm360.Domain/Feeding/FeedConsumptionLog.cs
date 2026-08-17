using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Events;

namespace Farm360.Domain.Feeding;

public sealed class FeedConsumptionLog : AuditableEntity, IAggregateRoot
{
    private readonly List<ConsumptionDetail> _details = new();

    public Guid FarmId { get; private set; }
    public Guid? ShedId { get; private set; }
    public Guid? PenId { get; private set; }
    public Guid? BatchId { get; private set; }
    public Guid FormulaId { get; private set; }
    public Guid? FeedingPlanId { get; private set; }

    public DateOnly LogDate { get; private set; }
    public int HeadCount { get; private set; }
    public decimal TotalFeedOfferedKg { get; private set; }
    public decimal TotalRefusalKg { get; private set; }
    public decimal NetConsumptionKg { get; private set; }
    public decimal TotalCostBdt { get; private set; }
    public string? LoggedByUserId { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyCollection<ConsumptionDetail> Details => _details.AsReadOnly();

    private FeedConsumptionLog() { } // EF Core

    public FeedConsumptionLog(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid formulaId,
        DateOnly logDate,
        int headCount,
        decimal totalFeedOfferedKg,
        decimal totalRefusalKg,
        decimal costPerKgBdt,
        Guid? shedId = null,
        Guid? penId = null,
        Guid? batchId = null,
        Guid? feedingPlanId = null,
        string? loggedByUserId = null,
        string? notes = null)
        : base(id, tenantId)
    {
        FarmId = farmId;
        FormulaId = formulaId;
        ShedId = shedId;
        PenId = penId;
        BatchId = batchId;
        FeedingPlanId = feedingPlanId;
        LogDate = logDate;
        HeadCount = Math.Max(1, headCount);
        TotalFeedOfferedKg = Math.Max(0, totalFeedOfferedKg);
        TotalRefusalKg = Math.Max(0, totalRefusalKg);
        NetConsumptionKg = Math.Max(0, TotalFeedOfferedKg - TotalRefusalKg);
        TotalCostBdt = Math.Round(NetConsumptionKg * Math.Max(0, costPerKgBdt), 2);
        LoggedByUserId = loggedByUserId;
        Notes = notes;

        RaiseDomainEvent(new FeedConsumptionLoggedEvent(
            Id, TenantId, FarmId, ShedId, PenId, FormulaId, LogDate, NetConsumptionKg, TotalCostBdt));
    }

    public void AddDetail(Guid ingredientId, decimal offeredKg, decimal refusalKg, decimal unitCostBdt)
    {
        _details.Add(new ConsumptionDetail(Guid.NewGuid(), Id, ingredientId, offeredKg, refusalKg, unitCostBdt));
    }
}

public sealed class ConsumptionDetail : BaseEntity
{
    public Guid LogId { get; private set; }
    public Guid IngredientId { get; private set; }
    public decimal OfferedKg { get; private set; }
    public decimal RefusalKg { get; private set; }
    public decimal NetConsumedKg { get; private set; }
    public decimal CostBdt { get; private set; }

    private ConsumptionDetail() { } // EF Core

    public ConsumptionDetail(Guid id, Guid logId, Guid ingredientId, decimal offeredKg, decimal refusalKg, decimal unitCostBdt)
        : base(id)
    {
        LogId = logId;
        IngredientId = ingredientId;
        OfferedKg = Math.Max(0, offeredKg);
        RefusalKg = Math.Max(0, refusalKg);
        NetConsumedKg = Math.Max(0, OfferedKg - RefusalKg);
        CostBdt = Math.Round(NetConsumedKg * Math.Max(0, unitCostBdt), 2);
    }
}
