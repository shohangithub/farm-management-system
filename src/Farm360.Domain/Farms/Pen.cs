using Farm360.Domain.Common;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Events;

namespace Farm360.Domain.Farms;

public sealed class Pen : AuditableEntity, IAggregateRoot
{
    private Pen() { }

    private Pen(
        Guid id,
        Guid tenantId,
        Guid shedId,
        string penNumber,
        string penName,
        int capacity,
        string? animalGroup,
        string? notes)
        : base(id, tenantId)
    {
        ShedId = shedId;
        PenNumber = penNumber;
        PenName = penName;
        Capacity = capacity;
        AnimalGroup = animalGroup;
        Notes = notes;
        CurrentOccupancy = 0;
        Status = PenStatus.Active;
    }

    public Guid ShedId { get; private set; }
    
    public string PenNumber { get; private set; } = string.Empty;
    public string PenName { get; private set; } = string.Empty;
    
    public int Capacity { get; private set; }
    public int CurrentOccupancy { get; private set; }
    
    public string? AnimalGroup { get; private set; }
    
    public string? Notes { get; private set; }
    
    public PenStatus Status { get; private set; }

    public static Pen Create(
        Guid tenantId,
        Guid shedId,
        string penNumber,
        string penName,
        int capacity,
        string? animalGroup,
        string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(penNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(penName);
        if (shedId == Guid.Empty) throw new ArgumentException("ShedId is required.", nameof(shedId));
        if (capacity < 0) throw new ArgumentException("Capacity cannot be negative.", nameof(capacity));

        var pen = new Pen(
            Guid.NewGuid(),
            tenantId,
            shedId,
            penNumber.Trim(),
            penName.Trim(),
            capacity,
            animalGroup?.Trim(),
            notes?.Trim());

        pen.RaiseDomainEvent(new PenCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            pen));

        return pen;
    }

    public void UpdateDetails(
        string penName,
        int capacity,
        string? animalGroup,
        string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(penName);
        if (capacity < 0) throw new ArgumentException("Capacity cannot be negative.", nameof(capacity));

        PenName = penName.Trim();
        Capacity = capacity;
        AnimalGroup = animalGroup?.Trim();
        Notes = notes?.Trim();

        RaiseDomainEvent(new PenUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            this));
    }

    public void ChangeStatus(PenStatus status)
    {
        if (Status == status) return;
        Status = status;

        RaiseDomainEvent(new PenUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            this));
    }

    public void UpdateOccupancy(int newOccupancy)
    {
        if (newOccupancy < 0) throw new ArgumentException("Occupancy cannot be negative.", nameof(newOccupancy));
        CurrentOccupancy = newOccupancy;
    }
}
