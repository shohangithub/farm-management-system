using System.Data.Common;
using Farm360.Application.Demo;
using Microsoft.EntityFrameworkCore;
using IdentityContext = Farm360.Identity.Context.IdentityDbContext;

namespace Farm360.Identity.Services;

/// <inheritdoc cref="IDemoAccountCleanup"/>
/// <remarks>
/// The identity tables live in the same database as the business data (schema <c>identity</c>), so
/// this opens a short-lived <see cref="IdentityContext"/> on the caller's connection and enlists it
/// in the caller's transaction: nothing here commits on its own. The shared demo Viewer is never
/// deleted (it is reused by every rebuild), and auth audit logs are insert-only and kept.
/// </remarks>
public sealed class DemoAccountCleanup : IDemoAccountCleanup
{
    public async Task<IReadOnlyCollection<Guid>> FindNonDemoUsersAsync(
        IReadOnlyCollection<Guid> userIds, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
            return [];

        await using var identity = Open(transaction);
        var users = await identity.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.IsSystemUser })
            .ToListAsync(cancellationToken);

        return users
            .Where(u => !IsDemoViewer(u.Id, u.UserName, u.IsSystemUser) && !IsDemoOwner(u.Id, u.UserName, u.IsSystemUser))
            .Select(u => u.Id)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, long>> DeleteDemoOwnersAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ownerUserIds, DbTransaction transaction,
        bool dryRun, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerUserIds);
        await using var identity = Open(transaction);

        // Re-check every id here: only a throwaway demo Owner is ever deleted.
        var requested = ownerUserIds.Where(id => id != DemoConstants.ViewerUserId && id != Guid.Empty).ToList();
        var users = await identity.Users.AsNoTracking()
            .Where(u => requested.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.IsSystemUser })
            .ToListAsync(cancellationToken);
        var ids = users.Where(u => IsDemoOwner(u.Id, u.UserName, u.IsSystemUser)).Select(u => u.Id).ToList();

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        async Task Run<T>(string table, IQueryable<T> rows)
        {
            counts[table] = dryRun
                ? await rows.LongCountAsync(cancellationToken)
                : await rows.ExecuteDeleteAsync(cancellationToken);
        }

        // Sessions of the purged tenant (any user, e.g. the Viewer's old ones), then the Owners' own rows.
        await Run("identity.UserSessions", identity.UserSessions.Where(s => s.TenantId == tenantId || ids.Contains(s.UserId)));
        await Run("identity.UserDevices", identity.UserDevices.Where(d => ids.Contains(d.UserId)));
        await Run("identity.ExternalProviders", identity.ExternalProviders.Where(p => ids.Contains(p.UserId)));
        await Run("identity.UserClaims", identity.UserClaims.Where(c => ids.Contains(c.UserId)));
        await Run("identity.UserLogins", identity.UserLogins.Where(l => ids.Contains(l.UserId)));
        await Run("identity.UserTokens", identity.UserTokens.Where(t => ids.Contains(t.UserId)));
        await Run("identity.UserRoles", identity.UserRoles.Where(r => ids.Contains(r.UserId)));
        await Run("identity.Users", identity.Users.Where(u => ids.Contains(u.Id)
                                                             && u.Id != DemoConstants.ViewerUserId
                                                             && !u.IsSystemUser));

        return counts;
    }

    private static bool IsDemoViewer(Guid id, string? userName, bool isSystemUser) =>
        id == DemoConstants.ViewerUserId
        && !isSystemUser
        && string.Equals(userName, DemoConstants.ViewerUserName, StringComparison.OrdinalIgnoreCase);

    private static bool IsDemoOwner(Guid id, string? userName, bool isSystemUser) =>
        id != DemoConstants.ViewerUserId && !isSystemUser && DemoConstants.IsDemoOwnerUserName(userName);

    private static IdentityContext Open(DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("The purge transaction has no connection.");

        var options = new DbContextOptionsBuilder<IdentityContext>()
            .UseSqlServer(connection)
            .Options;
        var context = new IdentityContext(options);
        context.Database.UseTransaction(transaction);
        return context;
    }
}
