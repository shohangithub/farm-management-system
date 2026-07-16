using Farm360.Domain.Common;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Events;

namespace Farm360.Domain.Farms;

public sealed class Shed : AuditableEntity, IAggregateRoot
{
    private Shed() { }

    private Shed(
        Guid id,
        Guid tenantId,
        Guid farmId,
        string shedNumber,
        string shedName,
        int? capacity,
        string? animalType,
        string? floorType,
        string? roofType,
        bool hasVentilation,
        bool hasWaterLine,
        bool hasFeedLine)
        : base(id, tenantId)
    {
        FarmId = farmId;
        ShedNumber = shedNumber;
        ShedName = shedName;
        Capacity = capacity;
        AnimalType = animalType;
        FloorType = floorType;
        RoofType = roofType;
        HasVentilation = hasVentilation;
        HasWaterLine = hasWaterLine;
        HasFeedLine = hasFeedLine;
        CurrentOccupancy = 0;
        Status = ShedStatus.Active;
    }

    public Guid FarmId { get; private set; }
    
    public string ShedNumber { get; private set; } = string.Empty;
    public string ShedName { get; private set; } = string.Empty;
    
    public int? Capacity { get; private set; }
    public int CurrentOccupancy { get; private set; }
    
    public string? AnimalType { get; private set; }
    public string? FloorType { get; private set; }
    public string? RoofType { get; private set; }
    
    public bool HasVentilation { get; private set; }
    public bool HasWaterLine { get; private set; }
    public bool HasFeedLine { get; private set; }
    
    public ShedStatus Status { get; private set; }

    public static Shed Create(
        Guid tenantId,
        Guid farmId,
        string shedNumber,
        string shedName,
        int? capacity,
        string? animalType,
        string? floorType,
        string? roofType,
        bool hasVentilation,
        bool hasWaterLine,
        bool hasFeedLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shedNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(shedName);
        if (farmId == Guid.Empty) throw new ArgumentException("FarmId is required.", nameof(farmId));

        var shed = new Shed(
            Guid.NewGuid(),
            tenantId,
            farmId,
            shedNumber.Trim(),
            shedName.Trim(),
            capacity,
            animalType?.Trim(),
            floorType?.Trim(),
            roofType?.Trim(),
            hasVentilation,
            hasWaterLine,
            hasFeedLine);

        shed.RaiseDomainEvent(new ShedCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            shed));

        return shed;
    }

    public void UpdateDetails(
        string shedName,
        int? capacity,
        string? animalType,
        string? floorType,
        string? roofType,
        bool hasVentilation,
        bool hasWaterLine,
        bool hasFeedLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shedName);

        ShedName = shedName.Trim();
        Capacity = capacity;
        AnimalType = animalType?.Trim();
        FloorType = floorType?.Trim();
        RoofType = roofType?.Trim();
        HasVentilation = hasVentilation;
        HasWaterLine = hasWaterLine;
        HasFeedLine = hasFeedLine;

        RaiseDomainEvent(new ShedUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            this));
    }

    public void ChangeStatus(ShedStatus status)
    {
        if (Status == status) return;
        Status = status;

        RaiseDomainEvent(new ShedUpdatedEvent(
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
