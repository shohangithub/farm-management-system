using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Finance;

public sealed class FarmAccountingSettingsRepository(ApplicationDbContext context) : IFarmAccountingSettingsRepository
{
    public Task<FarmAccountingSettings?> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default) =>
        context.FarmAccountingSettings.FirstOrDefaultAsync(s => s.FarmId == farmId, cancellationToken);

    public void Add(FarmAccountingSettings settings) => context.FarmAccountingSettings.Add(settings);
    public void Update(FarmAccountingSettings settings) => context.FarmAccountingSettings.Update(settings);
}

public sealed class BalanceSheetEntryRepository(ApplicationDbContext context) : IBalanceSheetEntryRepository
{
    public Task<BalanceSheetEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.BalanceSheetEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BalanceSheetEntry>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default) =>
        await context.BalanceSheetEntries
            .AsNoTracking()
            .Where(e => e.FarmId == farmId)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.Code)
            .ToListAsync(cancellationToken);

    public async Task<int> GetLastCodeNumberAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: a deleted entry's code must not be reused.
        var codes = await context.BalanceSheetEntries
            .IgnoreQueryFilters()
            .Where(e => e.FarmId == farmId)
            .Select(e => e.Code)
            .ToListAsync(cancellationToken);
        return CodeNumbers.Max(codes);
    }

    public void Add(BalanceSheetEntry entry) => context.BalanceSheetEntries.Add(entry);
    public void Update(BalanceSheetEntry entry) => context.BalanceSheetEntries.Update(entry);
    public void Remove(BalanceSheetEntry entry) => context.BalanceSheetEntries.Remove(entry);
}

public sealed class FixedAssetRepository(ApplicationDbContext context) : IFixedAssetRepository
{
    public Task<FixedAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.FixedAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FixedAsset>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default) =>
        await context.FixedAssets
            .AsNoTracking()
            .Where(a => a.FarmId == farmId)
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

    public async Task<int> GetLastCodeNumberAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        var codes = await context.FixedAssets
            .IgnoreQueryFilters()
            .Where(a => a.FarmId == farmId)
            .Select(a => a.Code)
            .ToListAsync(cancellationToken);
        return CodeNumbers.Max(codes);
    }

    public void Add(FixedAsset asset) => context.FixedAssets.Add(asset);
    public void Update(FixedAsset asset) => context.FixedAssets.Update(asset);
    public void Remove(FixedAsset asset) => context.FixedAssets.Remove(asset);
}

internal static class CodeNumbers
{
    /// <summary>Largest numeric suffix among codes like "FA-0012"; 0 when there are none.</summary>
    public static int Max(IEnumerable<string> codes) =>
        codes.Select(c => int.TryParse(c[(c.LastIndexOf('-') + 1)..], out var n) ? n : 0)
             .DefaultIfEmpty(0)
             .Max();
}
