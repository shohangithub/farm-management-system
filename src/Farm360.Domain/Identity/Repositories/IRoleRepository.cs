namespace Farm360.Domain.Identity.Repositories;

public interface IRoleRepository
{
    /// <summary>An active role the tenant may assign: a system role or one of the tenant's own roles.</summary>
    Task<Role?> GetAssignableAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>All active roles the tenant may assign (system roles + the tenant's custom roles).</summary>
    Task<IReadOnlyList<Role>> ListAssignableAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Permission codes granted by the role.</summary>
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid roleId, CancellationToken cancellationToken = default);
}
