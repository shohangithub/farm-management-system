using Farm360.Domain.Common;

namespace Farm360.Domain.MasterData.Locations;

public class Country : AuditableEntity, IAggregateRoot
{
    private Country() { }

    public Country(Guid id, Guid tenantId, string name, string code) 
        : base(id, tenantId)
    {
        Name = name;
        Code = code;
    }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
}
