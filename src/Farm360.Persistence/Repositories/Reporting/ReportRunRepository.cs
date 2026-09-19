using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Reporting;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Reporting;

public class ReportRunRepository : IReportRunRepository
{
    private readonly ApplicationDbContext _context;

    public ReportRunRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public void Add(ReportRun run) => _context.ReportRuns.Add(run);

    public async Task<ReportRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.ReportRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportRun>> GetRecentAsync(
        string? reportKey,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        // The tenant filter on ApplicationDbContext scopes this; no explicit TenantId predicate
        // is needed here, and adding one would silently diverge if that filter ever changes.
        var query = _context.ReportRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(reportKey))
        {
            query = query.Where(r => r.ReportKey == reportKey);
        }

        return await query
            .OrderByDescending(r => r.RequestedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
