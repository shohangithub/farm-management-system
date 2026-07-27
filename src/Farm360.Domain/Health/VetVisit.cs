using Farm360.Domain.Common;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Events;

namespace Farm360.Domain.Health;

/// <summary>
/// VetVisit Aggregate Root — tracking veterinarian visits to a farm.
/// </summary>
public sealed class VetVisit : BaseEntity, IAggregateRoot
{
    private VetVisit() { } // EF Core

    private VetVisit(
        Guid id,
        Guid tenantId,
        Guid farmId,
        string vetName,
        DateOnly visitDate,
        VetVisitType visitType,
        string? purpose,
        string? findings,
        string? recommendations,
        decimal? costBdt,
        DateOnly? nextVisitDate)
        : base(id)
    {
        TenantId = tenantId;
        FarmId = farmId;
        VetName = vetName;
        VisitDate = visitDate;
        VisitType = visitType;
        Purpose = purpose;
        Findings = findings;
        Recommendations = recommendations;
        CostBdt = costBdt;
        NextVisitDate = nextVisitDate;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public string VetName { get; private set; } = string.Empty;
    public DateOnly VisitDate { get; private set; }
    public VetVisitType VisitType { get; private set; }
    public string? Purpose { get; private set; }
    public string? Findings { get; private set; }
    public string? Recommendations { get; private set; }
    public decimal? CostBdt { get; private set; }
    public DateOnly? NextVisitDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static VetVisit Create(
        Guid tenantId,
        Guid farmId,
        string vetName,
        DateOnly visitDate,
        VetVisitType visitType,
        string? purpose,
        string? findings,
        string? recommendations,
        decimal? costBdt,
        DateOnly? nextVisitDate)
    {
        if (farmId == Guid.Empty)
            throw new ArgumentException("FarmId is required.", nameof(farmId));

        if (string.IsNullOrWhiteSpace(vetName))
            throw new ArgumentException("VetName is required.", nameof(vetName));

        var visit = new VetVisit(
            Guid.NewGuid(),
            tenantId,
            farmId,
            vetName.Trim(),
            visitDate,
            visitType,
            purpose?.Trim(),
            findings?.Trim(),
            recommendations?.Trim(),
            costBdt,
            nextVisitDate);

        visit.RaiseDomainEvent(new VetVisitCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            visit.Id,
            tenantId,
            farmId,
            visit.VetName,
            visitType,
            visitDate));

        return visit;
    }
}
