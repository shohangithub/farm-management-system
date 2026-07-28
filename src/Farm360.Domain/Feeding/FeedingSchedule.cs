using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Events;

namespace Farm360.Domain.Feeding;

public sealed class FeedingSchedule : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public Guid? ShedId { get; private set; }
    public Guid? PenId { get; private set; }
    public Guid? BatchId { get; private set; }
    public Guid FormulaId { get; private set; }

    public string Title { get; private set; } = null!;
    public decimal TargetQuantityKgPerHead { get; private set; }
    public ScheduleFrequency Frequency { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    private FeedingSchedule() { } // EF Core

    public FeedingSchedule(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid formulaId,
        string title,
        decimal targetQuantityKgPerHead,
        ScheduleFrequency frequency,
        DateOnly startDate,
        Guid? shedId = null,
        Guid? penId = null,
        Guid? batchId = null,
        DateOnly? endDate = null,
        string? notes = null)
        : base(id, tenantId)
    {
        FarmId = farmId;
        FormulaId = formulaId;
        Title = title;
        TargetQuantityKgPerHead = Math.Max(0.1m, targetQuantityKgPerHead);
        Frequency = frequency;
        StartDate = startDate;
        ShedId = shedId;
        PenId = penId;
        BatchId = batchId;
        EndDate = endDate;
        Notes = notes;
        IsActive = true;

        RaiseDomainEvent(new FeedingScheduleCreatedEvent(Id, TenantId, FarmId, FormulaId, StartDate));
    }

    public void UpdateSchedule(
        string title,
        Guid formulaId,
        decimal targetQuantityKgPerHead,
        ScheduleFrequency frequency,
        DateOnly startDate,
        DateOnly? endDate,
        string? notes)
    {
        Title = title;
        FormulaId = formulaId;
        TargetQuantityKgPerHead = Math.Max(0.1m, targetQuantityKgPerHead);
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
        Notes = notes;
    }

    public void SetActiveStatus(bool active)
    {
        IsActive = active;
    }
}
