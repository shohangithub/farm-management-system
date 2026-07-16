using Farm360.Domain.Common;

namespace Farm360.Domain.MasterData.Locations;

public class Union : AuditableEntity, IAggregateRoot
{
    private Union() { }

    public Union(Guid id, Guid tenantId, Guid upazilaId, string name) 
        : base(id, tenantId)
    {
        UpazilaId = upazilaId;
        Name = name;
    }

    public Guid UpazilaId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
