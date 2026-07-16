using Farm360.Domain.Common;

namespace Farm360.Domain.MasterData.Locations;

public class Division : AuditableEntity, IAggregateRoot
{
    private Division() { }

    public Division(Guid id, Guid tenantId, Guid countryId, string name) 
        : base(id, tenantId)
    {
        CountryId = countryId;
        Name = name;
    }

    public Guid CountryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
