using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record ShareDistributionSliceDto(
    string Label,
    int ShareCount,
    decimal Percentage,
    decimal ValueBdt,
    string ColorHex,
    bool IsOwner,
    bool IsAvailable
);

public record ShareMarketOverviewDto(
    Guid FarmId,
    bool IsConfigured,
    FarmShareConfigDto? Config,
    int TotalShareholdersCount,
    int TotalAllocatedShares,
    decimal TotalCapitalRaisedBdt,
    IReadOnlyList<ShareDistributionSliceDto> Distribution,
    IReadOnlyList<ShareHoldingDto> Shareholders,
    IReadOnlyList<ShareTransactionDto> RecentTransactions
);
