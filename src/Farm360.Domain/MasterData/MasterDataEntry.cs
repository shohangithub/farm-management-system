using Farm360.Domain.Common;
using Farm360.Domain.MasterData.Enums;
using Farm360.Domain.MasterData.Events;

namespace Farm360.Domain.MasterData;

public sealed class MasterDataEntry : AuditableEntity, IAggregateRoot
{
    private MasterDataEntry() { }

    private MasterDataEntry(
        Guid id,
        Guid tenantId,
        MasterDataType type,
        string name,
        string code,
        string? description,
        int displayOrder,
        bool isActive)
        : base(id, tenantId)
    {
        Type = type;
        Name = name;
        Code = code;
        Description = description;
        DisplayOrder = displayOrder;
        IsActive = isActive;
    }

    public MasterDataType Type { get; private set; }
    
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static MasterDataEntry Create(
        Guid tenantId,
        MasterDataType type,
        string name,
        string code,
        string? description = null,
        int displayOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var entry = new MasterDataEntry(
            Guid.NewGuid(),
            tenantId,
            type,
            name.Trim(),
            code.Trim().ToUpperInvariant(),
            description?.Trim(),
            displayOrder,
            true);

        entry.RaiseDomainEvent(new MasterDataEntryCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            entry));

        return entry;
    }

    public void UpdateDetails(
        string name,
        string? description,
        int displayOrder,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;

        RaiseDomainEvent(new MasterDataEntryUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            this));
    }
}
