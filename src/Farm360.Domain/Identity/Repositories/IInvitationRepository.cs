namespace Farm360.Domain.Identity.Repositories;

public interface IInvitationRepository
{
    void Add(Invitation invitation);

    /// <summary>Tracked invitation of the given tenant.</summary>
    Task<Invitation?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked invitation by token hash, across tenants (the accept flow is anonymous). The hash
    /// is the only lookup key, so this never exposes another tenant's data without the token.
    /// </summary>
    Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Untracked tenant id for a token hash; used to take the tenant lock before loading.</summary>
    Task<Guid?> FindTenantIdByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Pending invitations (including ones past expiry) of the tenant, newest first.</summary>
    Task<IReadOnlyList<Invitation>> ListPendingAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Pending, not-yet-expired invitations of the tenant (they hold a seat).</summary>
    Task<int> CountOpenAsync(Guid tenantId, DateTime nowUtc, Guid? excludingInvitationId = null, CancellationToken cancellationToken = default);

    /// <summary>True when an open invitation already exists for this phone or email.</summary>
    Task<bool> HasOpenForAsync(Guid tenantId, string? phone, string? email, DateTime nowUtc, CancellationToken cancellationToken = default);
}
