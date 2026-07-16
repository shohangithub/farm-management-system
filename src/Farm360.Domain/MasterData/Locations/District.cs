using Farm360.Domain.Common;

namespace Farm360.Domain.MasterData.Locations;

public class District : AuditableEntity, IAggregateRoot
{
    private District() { }

    public District(Guid id, Guid tenantId, Guid divisionId, string name) 
        : base(id, tenantId)
    {
        DivisionId = divisionId;
        Name = name;
    }

    public Guid DivisionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
