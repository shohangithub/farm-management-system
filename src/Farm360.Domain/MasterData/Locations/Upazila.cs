using Farm360.Domain.Common;

namespace Farm360.Domain.MasterData.Locations;

public class Upazila : AuditableEntity, IAggregateRoot
{
    private Upazila() { }

    public Upazila(Guid id, Guid tenantId, Guid districtId, string name) 
        : base(id, tenantId)
    {
        DistrictId = districtId;
        Name = name;
    }

    public Guid DistrictId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
