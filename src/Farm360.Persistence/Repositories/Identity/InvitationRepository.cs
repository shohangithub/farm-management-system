using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Identity;

/// <summary>
/// Invitation queries take the tenant explicitly and bypass the ambient tenant filter: the accept
/// flow is anonymous (no tenant in the request), and every query is scoped by tenant id or by the
/// token hash, which only the invitee holds.
/// </summary>
public sealed class InvitationRepository(ApplicationDbContext context) : IInvitationRepository
{
    public void Add(Invitation invitation) => context.Invitations.Add(invitation);

    public Task<Invitation?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) =>
        context.Invitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && !i.IsDeleted, cancellationToken);

    public Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.Invitations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && !i.IsDeleted, cancellationToken);

    public async Task<Guid?> FindTenantIdByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var ids = await context.Invitations.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.TokenHash == tokenHash && !i.IsDeleted)
            .Select(i => i.TenantId)
            .Take(1)
            .ToListAsync(cancellationToken);
        return ids.Count == 0 ? null : ids[0];
    }

    public async Task<IReadOnlyList<Invitation>> ListPendingAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.Invitations.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.LastSentAtUtc)
            .ToListAsync(cancellationToken);

    public Task<int> CountOpenAsync(Guid tenantId, DateTime nowUtc, Guid? excludingInvitationId = null, CancellationToken cancellationToken = default) =>
        context.Invitations.IgnoreQueryFilters()
            .CountAsync(i => i.TenantId == tenantId && !i.IsDeleted
                             && i.Status == InvitationStatus.Pending && i.ExpiresAtUtc > nowUtc
                             && (excludingInvitationId == null || i.Id != excludingInvitationId),
                cancellationToken);

    public Task<bool> HasOpenForAsync(Guid tenantId, string? phone, string? email, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (phone is null && email is null) return Task.FromResult(false);

        return context.Invitations.IgnoreQueryFilters()
            .AnyAsync(i => i.TenantId == tenantId && !i.IsDeleted
                           && i.Status == InvitationStatus.Pending && i.ExpiresAtUtc > nowUtc
                           && ((phone != null && i.Phone == phone) || (email != null && i.Email == email)),
                cancellationToken);
    }
}
