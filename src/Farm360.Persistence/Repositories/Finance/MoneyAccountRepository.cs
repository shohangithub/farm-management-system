using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Finance;

public class MoneyAccountRepository(ApplicationDbContext context) : IMoneyAccountRepository
{
    public Task<MoneyAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.MoneyAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MoneyAccount>> GetByFarmIdAsync(Guid farmId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var saved = await context.MoneyAccounts
            .Where(a => a.FarmId == farmId && (includeInactive || a.IsActive))
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        // Accounts added in this unit of work (e.g. the default created a moment ago) count too.
        var pending = context.ChangeTracker.Entries<MoneyAccount>()
            .Where(e => e.State == EntityState.Added && e.Entity.FarmId == farmId && (includeInactive || e.Entity.IsActive))
            .Select(e => e.Entity)
            .Where(a => saved.All(s => s.Id != a.Id));
        return saved.Concat(pending).ToList();
    }

    public async Task<MoneyAccount?> GetDefaultAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        var pending = context.ChangeTracker.Entries<MoneyAccount>()
            .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.FarmId == farmId && e.Entity.IsDefault)?.Entity;
        return pending ?? await context.MoneyAccounts.FirstOrDefaultAsync(a => a.FarmId == farmId && a.IsDefault, cancellationToken);
    }

    public void Add(MoneyAccount account) => context.MoneyAccounts.Add(account);

    public void Update(MoneyAccount account)
    {
        // Tracked rows are saved by change detection; attach only a detached one.
        if (context.Entry(account).State == EntityState.Detached)
            context.MoneyAccounts.Update(account);
    }
}
