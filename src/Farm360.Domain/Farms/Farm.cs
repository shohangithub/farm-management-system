using Farm360.Domain.Common;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Events;

namespace Farm360.Domain.Farms;

public sealed class Farm : AuditableEntity, IAggregateRoot
{
    private Farm() { }

    private Farm(
        Guid id,
        Guid tenantId,
        Guid branchId,
        string farmCode,
        string farmName,
        FarmType type,
        double? farmSize,
        double? landArea,
        double? latitude,
        double? longitude,
        string? mapPolygon,
        int? capacity,
        string? ownerId,
        string? managerId,
        string? description)
        : base(id, tenantId)
    {
        BranchId = branchId;
        FarmCode = farmCode;
        FarmName = farmName;
        Type = type;
        FarmSize = farmSize;
        LandArea = landArea;
        Latitude = latitude;
        Longitude = longitude;
        MapPolygon = mapPolygon;
        Capacity = capacity;
        CurrentAnimalCount = 0;
        OwnerId = ownerId;
        ManagerId = managerId;
        Description = description;
        Status = FarmStatus.Active;
    }

    public Guid BranchId { get; private set; }
    
    public string FarmCode { get; private set; } = string.Empty;
    public string FarmName { get; private set; } = string.Empty;
    
    public FarmType Type { get; private set; }
    
    public double? FarmSize { get; private set; } // e.g. in acres or hectares
    public double? LandArea { get; private set; }
    
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? MapPolygon { get; private set; } // GeoJSON
    
    public int? Capacity { get; private set; }
    public int CurrentAnimalCount { get; private set; }
    
    public string? OwnerId { get; private set; }
    public string? ManagerId { get; private set; }
    
    public FarmStatus Status { get; private set; }
    
    public string? Description { get; private set; }

    public static Farm Create(
        Guid tenantId,
        Guid branchId,
        string farmCode,
        string farmName,
        FarmType type,
        double? farmSize,
        double? landArea,
        double? latitude,
        double? longitude,
        string? mapPolygon,
        int? capacity,
        string? ownerId,
        string? managerId,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(farmCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(farmName);
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));

        var farm = new Farm(
            Guid.NewGuid(),
            tenantId,
            branchId,
            farmCode.Trim(),
            farmName.Trim(),
            type,
            farmSize,
            landArea,
            latitude,
            longitude,
            mapPolygon,
            capacity,
            ownerId,
            managerId,
            description);

        farm.RaiseDomainEvent(new FarmCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            farm));

        return farm;
    }

    public void UpdateDetails(
        string farmName,
        FarmType type,
        double? farmSize,
        double? landArea,
        double? latitude,
        double? longitude,
        string? mapPolygon,
        int? capacity,
        string? ownerId,
        string? managerId,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(farmName);

        FarmName = farmName.Trim();
        Type = type;
        FarmSize = farmSize;
        LandArea = landArea;
        Latitude = latitude;
        Longitude = longitude;
        MapPolygon = mapPolygon;
        Capacity = capacity;
        OwnerId = ownerId;
        ManagerId = managerId;
        Description = description;

        RaiseDomainEvent(new FarmUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            this));
    }

    public void ChangeStatus(FarmStatus status)
    {
        if (Status == status) return;
        Status = status;

        RaiseDomainEvent(new FarmUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            this));
    }

    public void UpdateAnimalCount(int newCount)
    {
        if (newCount < 0) throw new ArgumentException("Animal count cannot be negative.", nameof(newCount));
        CurrentAnimalCount = newCount;
    }
}
