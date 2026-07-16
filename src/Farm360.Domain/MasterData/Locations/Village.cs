using Farm360.Domain.Common;

namespace Farm360.Domain.MasterData.Locations;

public class Village : AuditableEntity, IAggregateRoot
{
    private Village() { }

    public Village(Guid id, Guid tenantId, Guid unionId, string name) 
        : base(id, tenantId)
    {
        UnionId = unionId;
        Name = name;
    }

    public Guid UnionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
