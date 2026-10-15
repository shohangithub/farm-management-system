using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance.Accounting;

namespace Farm360.Domain.Finance.Interfaces;

public interface IFarmAccountingSettingsRepository
{
    Task<FarmAccountingSettings?> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);
    void Add(FarmAccountingSettings settings);
    void Update(FarmAccountingSettings settings);
}

public interface IBalanceSheetEntryRepository
{
    Task<BalanceSheetEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BalanceSheetEntry>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);

    /// <summary>Highest numeric suffix of the farm's entry codes (BSE-0007 → 7), for the next code.</summary>
    Task<int> GetLastCodeNumberAsync(Guid farmId, CancellationToken cancellationToken = default);

    void Add(BalanceSheetEntry entry);
    void Update(BalanceSheetEntry entry);
    void Remove(BalanceSheetEntry entry);
}

public interface IFixedAssetRepository
{
    Task<FixedAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FixedAsset>> GetByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);

    /// <summary>Highest numeric suffix of the farm's asset codes (FA-0012 → 12), for the next code.</summary>
    Task<int> GetLastCodeNumberAsync(Guid farmId, CancellationToken cancellationToken = default);

    void Add(FixedAsset asset);
    void Update(FixedAsset asset);
    void Remove(FixedAsset asset);
}
