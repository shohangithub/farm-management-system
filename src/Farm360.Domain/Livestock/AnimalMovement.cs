using Farm360.Domain.Common;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Tracks the physical placement of an animal over time.
/// A child entity of the Animal aggregate.
/// </summary>
public sealed class AnimalMovement : AuditableEntity
{
    private AnimalMovement() { }

    internal AnimalMovement(
        Guid id,
        Guid tenantId,
        Guid animalId,
        Guid? shedId,
        Guid? penId,
        DateTime placedAtUtc,
        Guid placedBy,
        string? transferReason)
        : base(id, tenantId)
    {
        AnimalId = animalId;
        ShedId = shedId;
        PenId = penId;
        PlacedAtUtc = placedAtUtc;
        PlacedBy = placedBy;
        TransferReason = transferReason;
    }

    public Guid AnimalId { get; private set; }
    public Guid? ShedId { get; private set; }
    public Guid? PenId { get; private set; }
    
    public DateTime PlacedAtUtc { get; private set; }
    public Guid PlacedBy { get; private set; }
    
    public DateTime? RemovedAtUtc { get; private set; }
    public Guid? RemovedBy { get; private set; }
    
    public string? TransferReason { get; private set; }

    internal void MarkAsRemoved(DateTime removedAtUtc, Guid removedBy)
    {
        if (RemovedAtUtc.HasValue)
            throw new InvalidOperationException("Movement record is already closed.");

        RemovedAtUtc = removedAtUtc;
        RemovedBy = removedBy;
    }
}
