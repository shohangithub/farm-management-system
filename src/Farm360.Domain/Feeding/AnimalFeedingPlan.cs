using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Events;

namespace Farm360.Domain.Feeding;

public sealed class AnimalFeedingPlan : AuditableEntity, IAggregateRoot
{
    private readonly List<FeedingPlanExclusion> _exclusions = new();

    public Guid FarmId { get; private set; }
    public Guid? AnimalId { get; private set; }
    public Guid? BatchId { get; private set; }
    public Guid? ShedId { get; private set; }
    public Guid? PenId { get; private set; }
    public Guid FeedingRuleSetId { get; private set; }
    public FeedingPlanType PlanType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public FeedingPlanStatus Status { get; private set; }
    
    // Denormalized from the current active rule line for performance
    public Guid? CurrentRuleLineId { get; private set; }
    public decimal? CurrentConcentrateKgPerDay { get; private set; }
    public decimal? CurrentRoughageKgPerDay { get; private set; }
    public decimal? TriggeredByWeightKg { get; private set; }

    public IReadOnlyCollection<FeedingPlanExclusion> Exclusions => _exclusions.AsReadOnly();

    private AnimalFeedingPlan() { }

    public AnimalFeedingPlan(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid feedingRuleSetId,
        FeedingPlanType planType,
        DateOnly startDate,
        DateOnly? endDate,
        Guid? animalId = null,
        Guid? batchId = null,
        Guid? shedId = null,
        Guid? penId = null)
        : base(id, tenantId)
    {
        if (animalId == null && batchId == null && shedId == null && penId == null)
            throw new ArgumentException("At least one target (Animal, Batch, Shed, or Pen) must be specified.");

        FarmId = farmId;
        FeedingRuleSetId = feedingRuleSetId;
        PlanType = planType;
        StartDate = startDate;
        EndDate = endDate;
        AnimalId = animalId;
        BatchId = batchId;
        ShedId = shedId;
        PenId = penId;
        Status = FeedingPlanStatus.Active;

        RaiseDomainEvent(new FeedingPlanActivatedEvent(Guid.NewGuid(), DateTime.UtcNow, Id, TenantId, FarmId));
    }

    public void UpdateCurrentRule(Guid ruleLineId, decimal weightKg, decimal concentrateKgPerDay, decimal roughageKgPerDay)
    {
        CurrentRuleLineId = ruleLineId;
        TriggeredByWeightKg = weightKg;
        CurrentConcentrateKgPerDay = concentrateKgPerDay;
        CurrentRoughageKgPerDay = roughageKgPerDay;
    }

    public void Cancel()
    {
        if (Status == FeedingPlanStatus.Cancelled || Status == FeedingPlanStatus.Completed)
            return;
            
        Status = FeedingPlanStatus.Cancelled;
    }

    public void UpdateLocation(Guid? shedId, Guid? penId)
    {
        ShedId = shedId;
        PenId = penId;
    }

    public void Complete()
    {
        if (Status == FeedingPlanStatus.Cancelled || Status == FeedingPlanStatus.Completed)
            return;
            
        Status = FeedingPlanStatus.Completed;
    }

    public void AddExclusion(DateOnly date, string reason, DateOnly? resumesOn = null)
    {
        var exclusion = new FeedingPlanExclusion(Guid.NewGuid(), Id, date, reason, resumesOn);
        _exclusions.Add(exclusion);
    }
}

public sealed class FeedingPlanExclusion : BaseEntity
{
    public Guid AnimalFeedingPlanId { get; private set; }
    public DateOnly ExclusionDate { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateOnly? ResumesOn { get; private set; }

    private FeedingPlanExclusion() { }

    internal FeedingPlanExclusion(
        Guid id,
        Guid animalFeedingPlanId,
        DateOnly exclusionDate,
        string reason,
        DateOnly? resumesOn)
        : base(id)
    {
        AnimalFeedingPlanId = animalFeedingPlanId;
        ExclusionDate = exclusionDate;
        Reason = reason;
        ResumesOn = resumesOn;
    }
}
